# Validation

- Complete current Windows source and setup compile with the .NET Framework C# 5 compiler.
- 1,075 Windows regression checks pass. The installer title assertion was updated from 1.24 to the actual displayed 1.1; internal assembly remains 1.24.0.0.
- Root shared core/platform compile as a Framework library using C# 5.
- Avalonia app builds with .NET 10, with zero warnings and zero errors.
- Self-contained single-file Linux x64 publish succeeds with the Linux installer's publish properties.
- Linux core test project compiles with zero warnings and errors. Its Linux-specific runtime tests were not executed on this Windows host. WSL is not installed; the Ubuntu CI job is configured to run them.
- 90 schema checks pass with the same System.Text.Json path used by Linux. Unknown root and nested fields survive round trips and edits of known fields; old fixtures still read.
- All existing properties on nine shared persisted model classes still exist in the current Windows source.
- Root PNG/ICO are restored from the released artwork; Windows source retains independent copies.
- The installed Windows executable's SHA256 remains AD95500C680B75E519A53BCB74E19A6E4923922E9EC211F10915C26342302251. This task did not modify the Windows installation.

The Linux GUI and native launch behavior have not been exercised on Linux. The newer Windows features have not yet been ported into the root shared core. Source is supplied as-is in a separate Windows tree, with explicit documentation of that limitation.
