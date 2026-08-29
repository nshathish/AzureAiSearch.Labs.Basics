# AiSearch.Labs.Basics

A Blazor Server app for learning the Blob Storage &rarr; Indexer &rarr; Azure AI Search workflow.

## Adding Tailwind CSS

The UI was migrated from Bootstrap to [Tailwind CSS v4](https://tailwindcss.com/) using the standalone Tailwind CLI — no build framework (Vite/webpack) required, just Node for the CLI itself. Steps taken:

### 1. Initialize npm and install Tailwind

```bash
npm init -y
npm install -D tailwindcss @tailwindcss/cli
```

This created `package.json` and `node_modules/` at the project root (next to the `.csproj`).

### 2. Add npm scripts

In `package.json`:

```json
"scripts": {
  "build:css": "tailwindcss -i ./Styles/app.css -o ./wwwroot/css/app.css --minify",
  "watch:css": "tailwindcss -i ./Styles/app.css -o ./wwwroot/css/app.css --watch"
}
```

- `Styles/app.css` is the **source** file (not served, not built into the app).
- `wwwroot/css/app.css` is the **compiled output** that the app actually serves.

### 3. Create the Tailwind source file

`Styles/app.css`:

```css
@import "tailwindcss";

@source "../Components/**/*.razor";

@theme {
    --font-sans: "Inter", ui-sans-serif, system-ui, -apple-system, "Segoe UI", sans-serif;
    --color-brand-50: #eef2ff;
    /* ...brand color scale... */
}

@layer base {
    /* global element defaults, #blazor-error-ui, .blazor-error-boundary, .validation-message */
}

@layer components {
    /* reusable classes: .btn-primary, .card, .form-input, .badge-brand, .alert-info, .nav-pill, .file-input, etc. */
}
```

Key points for a Blazor project specifically:

- Tailwind v4 auto-scans the project for class names, but by default it skips common non-source folders. The explicit `@source "../Components/**/*.razor";` directive tells it to scan `.razor` files (Tailwind doesn't recognize `.razor` as a source extension out of the box).
- No `tailwind.config.js` is needed — v4 configures theme values via the `@theme` block directly in CSS.
- Reusable component classes (buttons, cards, badges, form controls) are defined once under `@layer components` with `@apply`, so `.razor` markup can use short semantic class names (`btn-primary`, `card`, `form-input`) instead of long utility strings everywhere.
  - **Gotcha**: Tailwind v4's `@apply` cannot reference another custom class that was itself built with `@apply` (e.g. a shared `.btn` base class chained into `.btn-primary`/`.btn-secondary`). Each variant needs its full utility list inlined, or the shared part needs to be declared with `@utility` instead of a plain class selector.

### 4. Build the CSS

```bash
npm run build:css
```

This generates `wwwroot/css/app.css`, which only contains the utility classes actually used in the `.razor` files (Tailwind's content-aware purging).

### 5. Reference the compiled CSS from the app

In `Components/App.razor`, replaced the Bootstrap `<link>` with the compiled Tailwind output, resolved through ASP.NET Core's static asset fingerprinting:

```razor
<link rel="stylesheet" href="@Assets["css/app.css"]" />
```

### 6. Remove Bootstrap

Deleted `wwwroot/lib/bootstrap/` and the old hand-written `wwwroot/app.css` (its remaining custom rules — `#blazor-error-ui`, `.blazor-error-boundary`, `.validation-message` — were ported into `Styles/app.css` under `@layer base` so they compile through Tailwind too).

## Day-to-day workflow

| Task | Command |
|---|---|
| Rebuild CSS once (e.g. after pulling changes) | `npm run build:css` |
| Watch and rebuild CSS while editing `.razor` files | `npm run watch:css` |
| Run the app | `dotnet run` |

> The Tailwind build is **not** wired into the `.csproj`/MSBuild pipeline. `wwwroot/css/app.css` is a committed, generated file — after changing any Tailwind classes in `.razor` markup or editing `Styles/app.css`, re-run `npm run build:css` (or keep `npm run watch:css` running) before `dotnet run`/`dotnet build`, otherwise new classes won't appear in the served stylesheet.

## Adding new utility classes

1. Edit the relevant `.razor` file and add Tailwind utility classes directly in markup, or add a new reusable class under `@layer components` in `Styles/app.css`.
2. Run `npm run build:css` (or have `watch:css` running).
3. Refresh the browser.
