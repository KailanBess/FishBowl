import SwiftUI
import UniformTypeIdentifiers

@main
@MainActor
struct FishBowlApp: App {
    @StateObject private var library = GameLibrary()
    @AppStorage("appearance") private var appearance = "System"

    var body: some Scene {
        WindowGroup {
            MobileShell(library: library)
                .preferredColorScheme(appearance == "Dark" ? .dark : appearance == "Light" ? .light : nil)
        }
    }
}

struct Game: Codable, Identifiable, Sendable {
    let id: UUID
    let name: String
    let filename: String
    let platform: String
    var favorite = false

    static let platforms = [
        "gb": "Game Boy", "gbc": "Game Boy Color", "gba": "Game Boy Advance",
        "nes": "NES", "fds": "Famicom Disk System", "sfc": "Super Nintendo",
        "smc": "Super Nintendo", "n64": "Nintendo 64", "z64": "Nintendo 64",
        "v64": "Nintendo 64", "nds": "Nintendo DS", "3ds": "Nintendo 3DS",
        "gen": "Genesis", "md": "Genesis", "sms": "Master System",
        "gg": "Game Gear", "pce": "PC Engine", "iso": "Disc image",
        "chd": "Disc image", "gcm": "GameCube", "wbfs": "Wii"
    ]
}

@MainActor
final class GameLibrary: ObservableObject {
    @Published private(set) var games: [Game] = []
    @Published private(set) var loadError: String?
    @Published private(set) var importing = false

    private let root = URL.applicationSupportDirectory.appending(path: "FishBowl", directoryHint: .isDirectory)
    private var index: URL { root.appending(path: "library.json") }
    private var storage: URL { root.appending(path: "Games", directoryHint: .isDirectory) }

    init() {
        guard FileManager.default.fileExists(atPath: index.path) else { return }
        do {
            games = try JSONDecoder().decode([Game].self, from: Data(contentsOf: index))
        } catch {
            loadError = "The saved library could not be read. Its data has been left in place."
        }
    }

    func importFiles(_ urls: [URL]) async -> String? {
        guard !importing, loadError == nil else { return nil }
        importing = true
        defer { importing = false }
        var failures = [String]()
        let destination = storage
        for url in urls {
            do {
                let game = try await Task.detached(priority: .userInitiated) {
                    try Self.copyGame(url, to: destination)
                }.value
                do {
                    try save(games + [game])
                } catch {
                    try? FileManager.default.removeItem(at: file(for: game))
                    throw error
                }
            } catch {
                failures.append("\(url.lastPathComponent): \(error.localizedDescription)")
            }
        }
        return failures.isEmpty ? nil : failures.joined(separator: "\n")
    }

    func toggleFavorite(_ game: Game) throws {
        guard let position = games.firstIndex(where: { $0.id == game.id }) else { return }
        var updated = games
        updated[position].favorite.toggle()
        try save(updated)
    }

    func file(for game: Game) -> URL {
        storage.appending(path: game.id.uuidString).appendingPathExtension(game.filenamePathExtension)
    }

    func remove(_ game: Game) throws {
        try save(games.filter { $0.id != game.id })
        let copy = file(for: game)
        if FileManager.default.fileExists(atPath: copy.path) {
            try FileManager.default.removeItem(at: copy)
        }
    }

    private func save(_ updated: [Game]) throws {
        guard loadError == nil else { throw CocoaError(.fileWriteUnknown) }
        try FileManager.default.createDirectory(at: root, withIntermediateDirectories: true)
        try JSONEncoder().encode(updated).write(to: index, options: .atomic)
        games = updated
    }

    private nonisolated static func copyGame(_ url: URL, to storage: URL) throws -> Game {
        let ext = url.pathExtension.lowercased()
        guard let platform = Game.platforms[ext] else { throw ImportError.unsupported }
        let accessed = url.startAccessingSecurityScopedResource()
        defer { if accessed { url.stopAccessingSecurityScopedResource() } }
        let coordinator = NSFileCoordinator()
        var coordinationError: NSError?
        var result: Result<Game, Error>?
        coordinator.coordinate(readingItemAt: url, options: [], error: &coordinationError) { source in
            result = Result {
                guard try source.resourceValues(forKeys: [.isRegularFileKey]).isRegularFile == true else {
                    throw ImportError.unsupported
                }
                let game = Game(id: UUID(), name: url.deletingPathExtension().lastPathComponent,
                                filename: url.lastPathComponent, platform: platform)
                let target = storage.appending(path: game.id.uuidString).appendingPathExtension(ext)
                try FileManager.default.createDirectory(at: storage, withIntermediateDirectories: true)
                do {
                    try FileManager.default.copyItem(at: source, to: target)
                } catch {
                    try? FileManager.default.removeItem(at: target)
                    throw error
                }
                return game
            }
        }
        if let coordinationError { throw coordinationError }
        guard let result else { throw CocoaError(.fileReadUnknown) }
        return try result.get()
    }

    private enum ImportError: LocalizedError {
        case unsupported
        var errorDescription: String? { "Choose a supported cartridge or disc image." }
    }
}

private extension Game {
    var filenamePathExtension: String { (filename as NSString).pathExtension.lowercased() }
}

@MainActor
struct MobileShell: View {
    @ObservedObject var library: GameLibrary
    @Environment(\.horizontalSizeClass) private var sizeClass
    @Environment(\.colorScheme) private var colorScheme
    @AppStorage("gameGrid") private var grid = true
    @AppStorage("appearance") private var appearance = "System"
    @AppStorage("accent") private var accent = "Mint"
    @State private var section = Section.games
    @State private var query = ""
    @State private var importer = false
    @State private var issue: String?
    @State private var pendingRemoval: Game?

    private enum Section: String, CaseIterable {
        case games = "My Games", favorites = "Favorites", settings = "Settings"
        var icon: String {
            switch self {
            case .games: "square.grid.2x2"
            case .favorites: "star"
            case .settings: "gearshape"
            }
        }
    }

    private var tint: Color {
        switch accent {
        case "Violet": .purple
        case "Ocean": .blue
        default: colorScheme == .dark ? Color(red: 0.36, green: 0.87, blue: 0.70) : Color(red: 0.04, green: 0.44, blue: 0.34)
        }
    }

    var body: some View {
        Group {
            if sizeClass == .regular {
                NavigationSplitView {
                    List {
                        ForEach(Section.allCases, id: \.self) { item in
                            Button {
                                section = item
                            } label: {
                                Label(item.rawValue, systemImage: item.icon)
                            }
                            .accessibilityAddTraits(section == item ? .isSelected : [])
                        }
                    }
                    .navigationTitle("FishBowl")
                } detail: {
                    NavigationStack { page(section) }
                        .id(section)
                }
            } else {
                TabView(selection: $section) {
                    ForEach(Section.allCases, id: \.self) { item in
                        NavigationStack { page(item) }
                            .tabItem { Label(item.rawValue, systemImage: item.icon) }
                            .tag(item)
                    }
                }
            }
        }
        .tint(tint)
        .fileImporter(isPresented: $importer, allowedContentTypes: [.data], allowsMultipleSelection: true) { result in
            switch result {
            case .success(let urls):
                Task { issue = await library.importFiles(urls) }
            case .failure(let error):
                issue = error.localizedDescription
            }
        }
        .alert("FishBowl", isPresented: Binding(get: { issue != nil }, set: { if !$0 { issue = nil } })) {
            Button("OK", role: .cancel) { issue = nil }
        } message: {
            Text(issue ?? "")
        }
        .confirmationDialog("Remove imported copy?", isPresented: Binding(
            get: { pendingRemoval != nil }, set: { if !$0 { pendingRemoval = nil } }
        ), presenting: pendingRemoval) { game in
            Button("Remove Game", role: .destructive) {
                do { try library.remove(game) }
                catch { issue = error.localizedDescription }
                pendingRemoval = nil
            }
        } message: { game in
            Text("FishBowl’s copy of \(game.name) will be removed. The original file stays in Files.")
        }
    }

    @ViewBuilder
    private func page(_ item: Section) -> some View {
        if item == .settings {
            Form {
                SwiftUI.Section("Appearance") {
                    Picker("Theme", selection: $appearance) {
                        ForEach(["System", "Dark", "Light"], id: \.self) { Text($0) }
                    }
                    Picker("Accent", selection: $accent) {
                        ForEach(["Mint", "Ocean", "Violet"], id: \.self) { Text($0) }
                    }
                    Toggle("Use game cards", isOn: $grid)
                }
                SwiftUI.Section("Library") {
                    LabeledContent("Imported games", value: "\(library.games.count)")
                    LabeledContent("Favorites", value: "\(library.games.filter(\.favorite).count)")
                    Text("Imported copies stay on this device.")
                        .foregroundStyle(.secondary)
                }
            }
            .navigationTitle(item.rawValue)
        } else {
            gamePage(item)
        }
    }

    private func gamePage(_ item: Section) -> some View {
        let collection = library.games.filter { item != .favorites || $0.favorite }
        let filtered = collection.filter {
            query.isEmpty || $0.name.localizedCaseInsensitiveContains(query)
                || $0.platform.localizedCaseInsensitiveContains(query)
        }
        return ScrollView {
            VStack(alignment: .leading, spacing: 16) {
                if let error = library.loadError {
                    Label(error, systemImage: "exclamationmark.triangle").foregroundStyle(.secondary)
                }
                if library.importing { ProgressView("Importing games…") }
                if filtered.isEmpty {
                    ContentUnavailableView(
                        query.isEmpty ? (item == .favorites ? "No favorites yet" : "Your library is empty") : "No matching games",
                        systemImage: item.icon,
                        description: Text(query.isEmpty && item == .games ? "Use Add Games to import files from Files." : "")
                    )
                } else {
                    LazyVGrid(columns: grid ? [GridItem(.adaptive(minimum: 148), spacing: 12)] : [GridItem(.flexible())], spacing: 12) {
                        ForEach(filtered) { game in
                            NavigationLink {
                                gameDetails(game.id)
                            } label: {
                                card(game)
                            }
                            .buttonStyle(.plain)
                        }
                    }
                }
            }
            .padding(16)
        }
        .background(Color(uiColor: .systemGroupedBackground))
        .searchable(text: $query, prompt: "Search games or platforms")
        .navigationTitle(item.rawValue)
        .toolbar {
            ToolbarItemGroup(placement: .topBarTrailing) {
                Button { grid.toggle() } label: {
                    Label(grid ? "List view" : "Grid view", systemImage: grid ? "list.bullet" : "square.grid.2x2")
                }
                Button { importer = true } label: {
                    Label("Add Games", systemImage: "plus")
                }
                .disabled(library.importing || library.loadError != nil)
            }
        }
    }

    private func card(_ game: Game) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack {
                Image(systemName: "gamecontroller").foregroundStyle(tint)
                Spacer()
                if game.favorite { Image(systemName: "star.fill").foregroundStyle(tint) }
            }
            Text(game.name).font(.headline).lineLimit(2)
            Text(game.platform).font(.caption.weight(.semibold))
                .padding(.horizontal, 8).padding(.vertical, 4)
                .background(tint.opacity(0.15), in: Capsule())
        }
        .frame(maxWidth: .infinity, minHeight: 86, alignment: .topLeading)
        .padding(12)
        .background(Color(uiColor: .secondarySystemGroupedBackground), in: RoundedRectangle(cornerRadius: 12))
        .overlay { RoundedRectangle(cornerRadius: 12).strokeBorder(.secondary.opacity(0.2)) }
    }

    @ViewBuilder
    private func gameDetails(_ id: UUID) -> some View {
        if let game = library.games.first(where: { $0.id == id }) {
            Form {
                SwiftUI.Section("Game") {
                    LabeledContent("Platform", value: game.platform)
                    LabeledContent("File", value: game.filename)
                    Button {
                        do { try library.toggleFavorite(game) }
                        catch { issue = error.localizedDescription }
                    } label: {
                        Label(game.favorite ? "Remove from Favorites" : "Add to Favorites",
                              systemImage: game.favorite ? "star.slash" : "star")
                    }
                    .disabled(library.loadError != nil)
                }
                SwiftUI.Section("Playback") {
                    Text("This preview organizes your library. Playing games requires an iOS emulator integration.")
                        .foregroundStyle(.secondary)
                }
                Button("Remove Game", role: .destructive) { pendingRemoval = game }
                    .disabled(library.loadError != nil)
            }
            .navigationTitle(game.name)
            .navigationBarTitleDisplayMode(.inline)
        }
    }
}
