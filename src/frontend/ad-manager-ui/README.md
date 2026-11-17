# ad-manager-ui

React 19 + TypeScript + Vite frontend for AD Manager. In production the built files are served by the ASP.NET Core API.

- `npm run dev` starts the Vite dev server (proxies `/api` to the backend)
- `npm run build` type-checks (`tsc -b`) and builds to `dist/`
- `npm run lint` runs oxlint
- `npm test` runs the Vitest suite once; `npm run preview` serves the production build
