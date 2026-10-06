# GitHub basics for FishBowl

A plain-language guide to how FishBowl is developed. You don't need to memorize it; your coding agent can explain any part when it comes up.

## The big picture

FishBowl lives on **GitHub**, a website that stores the project's code and its whole history, so two people (and their AI assistants) can work on it without overwriting each other. GitHub is the **one shared copy**. Everyone's computer keeps a copy that is kept in sync with it.

If work only exists on your computer, the other person can't see it, and it can be lost. So everything goes through GitHub.

## Words you'll see

| Word | What it means | Everyday comparison |
| --- | --- | --- |
| **Repository** (repo) | The project folder on GitHub, with every file and every past version. | A shared folder with unlimited undo. |
| **Commit** | A saved snapshot of changes, with a short note saying what changed. | Saving a document, with a note on what you edited. |
| **Push / pull** | Sending your commits up to GitHub / bringing the latest changes down. | Uploading / downloading the newest version. |
| **`main`** | The official version of the project. | The final copy everyone uses. |
| **Branch** | A separate copy of `main` to work on a change safely. | Making a draft copy before editing the original. |
| **Pull request** (PR) | A request to bring a branch's changes into `main`, where they can be reviewed first. | Handing in your draft and asking "can this go into the final version?" |
| **Checks** (CI) | Automatic tests GitHub runs on every pull request, building the Windows and Linux versions. Green ✓ means it works; red ✗ means something broke. | A spell-checker that runs before you send. |
| **Merge** | Accepting a pull request: its changes become part of `main`. | Approving the draft into the final copy. |
| **Issue** | A to-do or bug report on GitHub, with a number like #10. | A sticky note on the shared folder. |
| **Release** | A published version (like v1.25.8) with the `.exe` files people download. | A boxed version on the shelf. |

## How a change happens

1. **Start from the latest `main`,** so you build on everyone's newest work.
2. **Make a branch** for one change, e.g. `fix-library-covers`.
3. **Make the change and commit it,** then **push** the branch to GitHub.
4. **Open a pull request** into `main`. The checks run automatically.
5. **Look at the checks.** If they're green, it's safe; if they're red, the agent fixes it first.
6. **Merge the pull request** on GitHub, then delete the branch.
7. **Update `HANDOFF.md`,** so the other person (and their AI) knows what changed.

Your coding agent can do steps 1–4 and 7 for you. Step 6 is a button on GitHub that you can click yourself once you've looked at the change.

## Merging a pull request on GitHub

1. Open the pull request (your agent gives you the link, or use the **Pull requests** tab).
2. Read the description, and look at **Files changed** if you're curious.
3. Scroll down. Check that the checks are green, and that it says it will merge into **`main`**.
4. Click **Merge pull request**, then **Confirm merge**.
5. Click **Delete branch**.

## Things to avoid

- **Uploading files through the GitHub website** ("Add files via upload"). This skips branches, reviews and checks, and can overwrite other people's work.
- **Zips of source code or `.exe` files in the repository.** Code goes in as normal files; `.exe` files go on the **Releases** page.
- **Working in a copy outside the repository.** Other people can't see it, and it's easy to lose.

## When something goes wrong

- **A check is red:** ask your agent what failed and to fix it on the same branch.
- **You found a bug:** go to **Issues → New issue → Bug report**, and describe what happened.
- **GitHub sign-in fails on your computer:** run `gh auth login` in a terminal, or sign in through GitHub Desktop.
- **Not sure what happened:** ask your agent to explain it in plain words. That's part of its job.
