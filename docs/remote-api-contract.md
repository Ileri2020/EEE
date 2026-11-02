# EESimulator remote API contract

The MAUI application has no MongoDB or Gemini credentials. Configure an **HTTPS** service URL in Settings; implement and deploy these routes in a trusted API server. Keep database and Gemini secrets only in that server's secret manager or environment configuration. A mobile build is public to its users and is not a secret store.

## Community examples

- `GET /api/examples` returns a JSON array with `id`, `title`, `category`, optional `icon`, `formula`, `description`, `componentSummary`, `ownerName`, and `contentJson` (native EEE schematic JSON).
- `POST /api/examples` requires a user-scoped `Authorization: Bearer <token>` issued by the API's sign-in system and accepts `title`, `category`, optional `icon`, `description`, and `contentJson`. Validate and size-limit the schematic before storing it; return the saved example using the same fields as GET. The MAUI client publishes reusable user circuits with category `Custom Components`; their EEE description includes versioned exposed-port metadata, and the optional `icon` is a sanitized inline SVG string.

The app's “Community” action refreshes and downloads the returned public examples. “Share Circuit” posts the current circuit. The app does not expose MongoDB credentials or grant database access to clients.

## AI script drafts

- `POST /api/ai/schema-script` requires the same user bearer token. The request contains `systemInstructions`, `prompt`, `currentScript`, truncated `lastScriptExecutionOutput` (latest command errors and simulation output), sanitized `currentCircuitJson`, `availableComponents`, `currentSchematicState`, `latestSimulation`, and optional `requestedComponentDetails`.
- `availableComponents` is the full runtime component registry with stable IDs, descriptions, defaults, prototype pins and writable scalar properties. `currentSchematicState` includes each placed component's actual pins, values, safe custom properties, connections and digital output levels. `latestSimulation` includes success/status, all operating-point net voltages and currents, and per-component readings. The client reruns operating-point simulation whenever the serialized schematic differs from the snapshot that produced the previous results.
- Return one JSON object: `{ "script": "...", "summary": "...", "warnings": [], "questions": [], "askComponentDetails": [{ "componentName": "U1", "fields": ["pins", "properties"] }] }`. The client answers at most eight detail requests per round and makes at most three AI calls for a draft. Detail requests receive the exact selected instance or type pins and writable scalar properties.
- Require sensible request-size limits and rate limits. Treat the prompt and submitted circuit as untrusted input. Return a Python draft only; the app places it in the editor and does **not** automatically run it.
- Generate only statements supported by the workspace's `eesim.*` dispatcher, such as `eesim.add_component("Resistor", "R1", 100, 100, 1000)`, `eesim.connect("V1", "positive", "R1", "pin1")`, `eesim.set_position("R1", 150, 100)`, `eesim.set_component_property("R1", "Resistance", 2200)`, and `eesim.set_logic("IN1", true)`. Scalar properties are applied through an undoable command; component identity, pins/topology, position and internal collections are not settable through that property command. Arbitrary Python execution is not provided by the app.
- Gemini keys must be loaded and rotated server-side. On provider authentication, quota, or transient errors, the server may try another configured Gemini key; never send keys to this client.

## Prisma/MongoDB deployment note

Prisma is not included in this .NET MAUI application. The API can be a separate Node/Next.js service using Prisma's MongoDB connector, with a server-only environment entry such as `DATABASE_URL="mongodb+srv://<user>:<password>@<cluster>/EEE?..."`. Use newly rotated credentials, apply least-privilege database access, and do not commit the real URI. For MongoDB, Prisma schema synchronization uses `prisma db push` rather than relational migration history.

## Safe API smoke test

1. Deploy the API and configure its HTTPS base URL plus a user-scoped access token in the app's Settings.
2. Verify public browsing with `GET /api/examples` (expected `200` and a JSON array).
3. Send an authenticated schema request with a harmless prompt like “Add one 1 kΩ resistor named R_TEST at (100, 100)” and verify the response is valid JSON, uses registered component/pin data, and contains only supported `eesim.*` statements. The app deliberately does not execute the generated script until a user reviews and presses Run.
4. Change the circuit, run the script, and request another draft; verify the request's `latestSimulation` reports new voltages/currents rather than stale measurements.

No live AI API test is possible from this repository alone: there is no deployed API URL or server credentials here. The app client contract can be inspected locally, but Gemini requests must be tested against the deployed authenticated service.

The client routes are ready to connect, but community sharing and AI remain unavailable until this API and its sign-in system are deployed, an HTTPS base URL is configured, and the user saves a scoped access token in Settings.
