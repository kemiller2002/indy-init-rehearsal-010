# Deployment

Conditor scaffolded a GitHub Pages deployment, `.github/workflows/deploy-pages.yml`.
It stays inert until a deployment target is chosen.

## Status

The target is not decided. The Indy-init planning set prefers GitHub Pages
"where practical" (D-203) and leaves the decision open (RQR-005). A static
Pages site cannot hold secrets: an AI provider key or an OAuth token exchange
needs a backend, which this workflow does not provide.

## Contract

- `npm run build` writes the complete deployable site to `dist/`, including
  `dist/index.html`. The scaffold defines no `build` script; add one with the
  first bundling decision.
- Every push to `main` deploys `dist/` once the workflow is enabled.

## Test data connection (HELD: owner decision ODR-COMP-P13 / ODR-R10-36)

This branch is held until the owner records the exception. With it, the deploy
build alone receives the secrets `REPO_OWNER`, `DATA_SOURCE`, `CAUSA_BRANCH` and
`DATA_ACCESS_KEY_CAUSA` and `CAUSA_DEPLOY_CONNECTION=1`; `build.fsx` writes
`dist/deploy-connection.json` (`build/DeployConnection.fs`), and the app connects
to `REPO_OWNER/DATA_SOURCE@CAUSA_BRANCH` at load without the key being typed.
Every other build writes the file without a connection and shows the Connect form.
The build, and a separate workflow step before upload, scan `dist/` and fail on
any credential but that one key in that one file. **The site is public: anyone can
read the key from it.** Use a fine-grained token on that one repository with
Contents read and write only, a short expiry, and revoke it when the test ends.

## Enable GitHub Pages

1. Settings, Pages: set the source to "GitHub Actions"
   (`gh api -X POST repos/OWNER/REPO/pages -f build_type=workflow`).
2. Set the repository variable: `gh variable set DEPLOY_TARGET --body github-pages`.
3. Push to `main`, or run the workflow from the Actions tab.

To choose another target, replace the workflow and update this file in the
same change.
