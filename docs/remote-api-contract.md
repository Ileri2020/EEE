# EESimulator remote API contract

The MAUI application has no MongoDB credentials. Configure an **HTTPS** service URL in Settings and deploy these routes in a trusted API server. Personal user keys are encrypted with AES-256-GCM and stored in the HCVP database; shared Gemini keys stay in the server's environment configuration. For direct app-to-Gemini requests, HCVP returns keys over HTTPS just in time; the app uses them in memory and does not persist them in device storage. App-runtime access can still expose a key.

## Community examples

- `GET /api/examples` returns a JSON array with `id`, `title`, `category`, optional `icon`, `formula`, `description`, `componentSummary`, `ownerName`, and `contentJson` (native EEE schematic JSON).
- `POST /api/examples` requires a user-scoped `Authorization: Bearer <token>` issued by the API's sign-in system and accepts `title`, `category`, optional `icon`, `description`, and `contentJson`. Validate and size-limit the schematic before storing it; return the saved example using the same fields as GET. The MAUI client publishes reusable user circuits with category `Custom Components`; their EEE description includes versioned exposed-port metadata, and the optional `icon` is a sanitized inline SVG string.

The app's “Community” action refreshes and downloads the returned public examples. “Share Circuit” posts the current circuit. The app does not expose MongoDB credentials or grant database access to clients.

## Private project saves

- `POST /api/projects` accepts `{ "title": "...", "description": "...", "contentJson": "..." }` and returns `{ "id": "...", "title": "...", "ownerName": null, "savedAt": "..." }`.
- Authentication is optional. When a valid bearer token is supplied, the HCVP server derives the account ID from the verified token; otherwise a valid same-origin HCVP browser session may provide the account identity. Never trust an owner/account ID sent in the request body. Without either identity, save the project as an anonymous record with no account association.
- Validate and size-limit `contentJson`, store it in the server database, and avoid returning it from public example listings. Anonymous save abuse should be mitigated with rate limits and request limits. The MAUI client now sends this request from its cloud-save action, attaching a saved token when available.
- HCVP implements this route in `app/api/projects/route.ts` and stores records in its `SavedEeeProject` MongoDB model. The MAUI cloud-save client attaches the saved EEE token when available; anonymous requests do not need one.

## AI script drafts

- `GET /api/eee/ai-key/client` returns an ordered `apiKeys` list and model with `Cache-Control: no-store`. With a valid EEE app bearer token, the user's decrypted personal key is first, followed by server keys for failover. Without a token, it returns the rotated HCVP `.env` key list. This endpoint intentionally exposes those shared keys to unauthenticated callers; anyone who can reach it can copy and use them. Do not treat `.env` keys returned by this endpoint as secret.
- The app sends circuit prompts directly to Google Gemini using each returned key in order, moving to the next key after a provider failure. Circuit data is not posted to HCVP for AI generation. `POST /api/ai/schema-script` is disabled and returns `410 Gone`. The app requests keys once per draft, then handles component-detail follow-up rounds directly with Gemini.
- `availableComponents` is the full runtime component registry with stable IDs, descriptions, defaults, prototype pins and writable scalar properties. `currentSchematicState` includes each placed component's actual pins, values, safe custom properties, connections and digital output levels. `latestSimulation` includes success/status, all operating-point net voltages and currents, and per-component readings. The client reruns operating-point simulation whenever the serialized schematic differs from the snapshot that produced the previous results.
- Return one JSON object: `{ "script": "...", "summary": "...", "warnings": [], "questions": [], "askComponentDetails": [{ "componentName": "U1", "fields": ["pins", "properties"] }] }`. The client answers at most eight detail requests per round and makes at most three AI calls for a draft. Detail requests receive the exact selected instance or type pins and writable scalar properties.
- Require sensible request-size limits and rate limits. Treat the prompt and submitted circuit as untrusted input. Return a Python draft only; the app places it in the editor and does **not** automatically run it.
- Generate only statements supported by the workspace's `eesim.*` dispatcher, such as `eesim.add_component("Resistor", "R1", 100, 100, 1000)`, `eesim.connect("V1", "positive", "R1", "pin1")`, `eesim.set_position("R1", 150, 100)`, `eesim.set_component_property("R1", "Resistance", 2200)`, and `eesim.set_logic("IN1", true)`. Scalar properties are applied through an undoable command; component identity, pins/topology, position and internal collections are not settable through that property command. Arbitrary Python execution is not provided by the app.
- `PUT /api/eee/ai-key` accepts a user's key over HTTPS and encrypts it with AES-256-GCM before database storage. `GET` reports configuration only; `DELETE` removes it. The client key endpoint decrypts a saved key only to include it in the transient response used by the app.
- Shared Gemini keys remain in server environment configuration at rest, but the client key endpoint exposes them to unauthenticated callers by design. Rotate any key that has been exposed and monitor provider usage.

## Prisma/MongoDB deployment note

Prisma is not included in this .NET MAUI application. The API can be a separate Node/Next.js service using Prisma's MongoDB connector, with a server-only environment entry such as `DATABASE_URL="mongodb+srv://<user>:<password>@<cluster>/EEE?..."`. Use newly rotated credentials, apply least-privilege database access, and do not commit the real URI. For MongoDB, Prisma schema synchronization uses `prisma db push` rather than relational migration history.

## Safe API smoke test

1. Deploy the API and configure its HTTPS base URL plus a user-scoped access token in the app's Settings.
2. Verify public browsing with `GET /api/examples` (expected `200` and a JSON array).
3. From the app, request a harmless draft such as “Add one 1 kΩ resistor named R_TEST at (100, 100)”. Verify HCVP is contacted only for the no-store key response, the app sends the circuit prompt directly to Gemini, and the resulting draft contains only supported `eesim.*` statements. The app does not execute the generated script until a user reviews and presses Run.
4. With an authenticated account and saved personal key, verify the endpoint returns that key first and HCVP server keys afterward, then verify the app calls Gemini directly and tries each key on failure. With no token, verify the endpoint returns the rotated server-key list and the app calls Gemini directly. Confirm AI prompt/circuit requests do not reach HCVP.
5. Change the circuit, run the script, and request another draft; verify the request's `latestSimulation` reports new voltages/currents rather than stale measurements.

No live AI API test is possible from this repository alone: there is no deployed API URL or server credentials here. The app client contract can be inspected locally, but Gemini requests must be tested against the deployed authenticated service.

Community sharing and signed-in personal-key Gemini requests require this API and sign-in system to be deployed, an HTTPS base URL configured, and a scoped access token saved in Settings. Anonymous direct Gemini requests use the server-key list returned by HCVP; this intentionally makes those keys extractable to endpoint callers.
