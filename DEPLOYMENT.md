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

## Enable GitHub Pages

1. Settings, Pages: set the source to "GitHub Actions"
   (`gh api -X POST repos/OWNER/REPO/pages -f build_type=workflow`).
2. Set the repository variable: `gh variable set DEPLOY_TARGET --body github-pages`.
3. Push to `main`, or run the workflow from the Actions tab.

To choose another target, replace the workflow and update this file in the
same change.
