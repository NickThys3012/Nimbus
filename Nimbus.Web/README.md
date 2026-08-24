# NimbusWeb

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.2.20.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Debugging in WebStorm (current setup)

The frontend is typically served by the backend process in this setup.

### WebStorm + backend-hosted SPA (primary flow)

1. Build the frontend:
   ```bash
   npm run build
   ```
2. Ensure the backend serves the Angular build from its `wwwroot`.
3. Start/debug the backend from your backend IDE.
4. In WebStorm, create a **JavaScript Debug** configuration:
   - **Run | Edit Configurations...**
   - **+** -> **JavaScript Debug**
   - **URL**: your backend URL (for example `https://localhost:5214`)
5. Start that WebStorm debug configuration.
6. Set breakpoints in TypeScript files (for example `src/app/Nimbus.ts` or files under `src/app/pages/`) and reproduce the issue in the browser window opened by WebStorm.

Notes:

- In production mode the frontend uses same-origin API calls (`OpenAPI.BASE = ''`) in `src/app/Nimbus.config.ts`.
- If breakpoints do not bind, hard-refresh and restart the WebStorm debug session.

### WebStorm + `ng serve` (optional fallback)

Use this when you specifically want frontend-only hot reload:

```bash
npm start
```

Then create a WebStorm **JavaScript Debug** config with URL `http://localhost:4200/` and start debugging there.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Regenerating the API client

Typed models and Angular services for the Nimbus API are generated from its
OpenAPI spec using [openapi-typescript-codegen](https://github.com/ferdikoomen/openapi-typescript-codegen)
and live in `src/app/core/api-client/` (committed to git).

To regenerate after the API contract changes:

1. Start the API locally (e.g. from Rider) so it's listening on
   `http://localhost:5214`.
2. Run:
   ```bash
   npm run generate:api
   ```
3. Review the diff in `src/app/core/api-client/` and commit the changes.

The generated `OpenAPI.BASE` defaults to `http://localhost:5214` and is also
set explicitly in `src/app/Nimbus.config.ts`.

## Using signals with the generated client

`openapi-typescript-codegen` only produces Observable-based services (RxJS),
not signals — that's a hard limitation of the tool, not something a codegen
flag can change. To get signals, add a thin handwritten wrapper on top of
the generated service, rather than editing generated files (they get
overwritten on every `npm run generate:api`).

See `src/app/core/auth/auth.store.ts` for the pattern, built on
`AuthenticationService`:

- **Mutations** (login/logout/refresh) are one-off actions: call the
  generated Observable-returning method, `.subscribe()`, and push the result
  into writable signals (`signal()`/`computed()`).
- **Reactive queries** (e.g. GET-by-email lookups) use `rxResource()` from
  `@angular/core/rxjs-interop`, which wraps an Observable-returning function
  and exposes `.value()`, `.isLoading()`, `.error()` as signals that
  automatically refetch when their `params` signal changes.

Apply the same pattern for any other generated service you want to consume
via signals.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
