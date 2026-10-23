# RavenNest.LoadTest

A load tester for the RavenNest server. It plays one or more streamers running Ravenfall, the way the
Unity client does, against a server on this machine, and then checks whether the server kept what it
was sent ("were saves lost?").

What one simulated streamer session does:

1. Signs in (`POST api/auth`) and begins a game session (`POST api/game/{version}/{accessKey}/{time}`),
   exactly as the game does. The tokens go base64 encoded JSON in the `auth-token` and `session-token`
   headers.
2. Connects the game event socket (TCP 3920: Telepathy framing, MessagePack `TypedPacket` auth), counts
   every event the server sends by type, and reconnects every 5 s after a drop like the game. Connecting
   this socket is also what starts the server's GameProcessor for the session.
3. Connects the delta socket (TCP 3921: `[length:4 BE][type:1][payload]`, auth frame = the session token).
4. Joins N players through `POST api/players` (what a chat `!join` turns into), in batches, or with
   `--join-mode restore` in one `POST api/players/restore` request (what the game does after a restart).
5. Every tick (2 s, the game's `GameSyncInterval`) sends experience, player state and game state frames
   with the game's exact encoding (ported from `DeltaClient.cs`): most players gain exp in one skill, or
   two when fighting (the combat skill plus Health); a share of players changes state (exp per hour and
   the level up estimate, often position and health, now and then the training skill). The first state
   send and the one after a reconnect carry every field, as in the game.
6. Optionally fires REST commands at a rate: add item, produce, craft, equip best, fetch player, highscore.
7. Reports every 10 s and at the end: requests per second and p50/p95/p99 per endpoint, failures and
   their messages, delta frames and bytes, events received by type, socket drops and reconnects.
8. Verifies: reads back a sample of players with `GET api/players/{characterId}` and compares every
   skill with the last level and exp the tool actually wrote to the delta socket. The server only ever
   raises a skill, so anything lower is an update the server lost. With `--verify-db` it also reads the
   rows straight from the database (read only) and waits until the server has written them.

## Safety

* It refuses to run unless the url and the socket host are `localhost`, `127.0.0.1` or `::1`.
  `--i-know-this-is-not-production` overrides that; do not use it against production.
* It creates users, characters and items in the database the server uses. Only point it at a server
  running on the dev database copy.
* Secrets come from environment variables. Nothing prints the password, access key, tokens or
  connection string. The command line works too, but other processes can read it.

## Setup

### 1. The test streamer account (one time)

The server cannot create a user with a password without a Twitch sign in, and a streamer needs a
password to sign in from the game. So this is the one step that goes to the database directly. The
players are not created here: the load test creates them through the server's own join path, which
makes a user and a character for any player it does not know.

```powershell
cd G:\Ravenfall\Projects\RavenNest-loadtest
$env:RAVENNEST_LOADTEST_PASSWORD = '<pick a password for the test streamers>'
dotnet run --project src/RavenNest.LoadTest -- seed --sessions 4
```

This writes `loadtest-seed.sql`, which creates `loadtest_streamer1` to `loadtest_streamer4` (enough for
up to `--sessions 4`): a `[User]` row with that password hashed by the server's own `SecureHasher`, an
empty `[Resources]` row, hidden from the highscore, no Twitch link. It is safe to run twice (it then only
resets the password).

**Stop the server, run the script against the dev database, start the server.** The server keeps every
row in memory and only knows the rows that existed when it started. Either run the file yourself
(SSMS or `sqlcmd`), or let the tool run it:

```powershell
$env:RAVENNEST_LOADTEST_DB = '<dev database connection string>'
dotnet run --project src/RavenNest.LoadTest -- seed --sessions 4 --execute
```

### 2. What each run needs

| what | where |
| --- | --- |
| `RAVENNEST_LOADTEST_PASSWORD` | the password used for the seed |
| `RAVENNEST_ACCESS_KEY` | the game's access key: the Access Key field on the GameManager in the Unity scene (it must match the `AccessKey` of the server's game client row) |
| `--client-version` | defaults to `0.9.5.0a` (the Unity project's version). BeginSession refuses a version older than the server expects; the error says which version it wanted |
| `RAVENNEST_LOADTEST_DB` | only for `--verify-db`: the dev database connection string (only SELECTs are run) |

Instead of the password, a single session can use an `auth-token` header value from
`RAVENNEST_LOADTEST_AUTH_TOKEN` (or `--auth-token`).

### 3. The server

Run the server from the worktree or repo you want to measure, on the dev database:
`ASPNETCORE_ENVIRONMENT=Development dotnet run -c Release --no-launch-profile` from `src/RavenNest.Blazor`
(it listens on 80/443 with the sockets on 3920/3921). Prefer Release for numbers: a Debug build is
slower and logs more (every TCP packet, every failed join).

The default url is `https://127.0.0.1`, the same address the game's local server mode uses; the local
development certificate is accepted (the host has already been checked to be this machine).

## Running

Check the encoding first (no network, compares with the server's own parsers):

```powershell
dotnet run --project src/RavenNest.LoadTest -- self-test
```

A 1000 player streamer for 10 minutes with some REST commands, keeping the numbers as JSON:

```powershell
$env:RAVENNEST_LOADTEST_PASSWORD = '<the seed password>'
$env:RAVENNEST_ACCESS_KEY = '<the game access key>'
dotnet run -c Release --project src/RavenNest.LoadTest -- run --players 1000 --duration 10m --command-rate 5 --json-report loadtest-1000.json
```

Other useful runs:

```powershell
# four streamers with 500 players each
dotnet run -c Release --project src/RavenNest.LoadTest -- run --sessions 4 --players 500 --duration 10m

# a restart storm: the same 2000 players come back in one restore request
dotnet run -c Release --project src/RavenNest.LoadTest -- run --players 2000 --join-mode restore --duration 5m

# also check what reached the database
dotnet run -c Release --project src/RavenNest.LoadTest -- run --players 1000 --verify-db

# what the game client does today: a new HTTP connection (and TLS handshake) for every request
dotnet run -c Release --project src/RavenNest.LoadTest -- run --players 1000 --new-connection-per-request

# the game's reconnect every minute: a new event socket signs in, then the old one closes
dotnet run -c Release --project src/RavenNest.LoadTest -- run --players 1000 --event-reconnect-every 1m
```

`help` lists every option. Durations take `ms`, `s`, `m` or `h`. `--duration` counts from the moment the
session started, so it includes the join phase. Ctrl+C stops the load early and still verifies; a second
Ctrl+C aborts.

### Players

Session `n` uses players `loadtest_s<n>_00001`, `loadtest_s<n>_00002`, ... on platform `twitch` with
platform ids `loadtest-s<n>-00001` (never numeric, so they cannot clash with a real Twitch id). The first
run creates them (users, characters, skills, loyalty); later runs reuse them, which is a different load
(the first run is insert heavy). `--player-prefix` gives a fresh set. Character ids are remembered in
`loadtest-rosters/` so `--join-mode restore` can send them all in one request like the game.

### Output and exit code

Every 10 s: per endpoint requests per second, ok/failed and p50/p95/p99 for the last interval; delta
frames and bytes and how long socket writes took; events received; drops. At the end the same for the
whole run, the top failure messages per endpoint, events by type, connections, join times, ticks that
took longer than the tick interval, and the verification.

Exit code 0 when fine, 2 when verification found players whose saved exp is behind what was sent
(after a recheck), 1 when the run could not start.

## The v2 connection (`--protocol v2`)

With `--protocol v2` a session plays the game the way the v2 client will (docs/network-v2.md in the game
repo). Signing in and BeginSession stay on REST; BeginSession must return a `ConnectionToken`, otherwise the
run stops with a message (the server has no v2). Then one WebSocket to `wss://<host>/api/v2/game` replaces
both the delta socket and the event socket:

* **Hello and resume.** The session sends `Hello` with the token, a resume id made once per run, and the
  last event it applied. After a drop it reconnects with the same resume id; the report counts resumes and
  reconnects the server did not resume.
* **Saves.** Each player has an outbox: every tick that changes a player makes a new version. Every tick a
  numbered `SaveBatch` carries every player with a new version, with every change since the last stored
  version (all skills and fields after joining or a resync). The server answers each batch once with `SaveAcks`
  Accepted (`ThroughSequence`, listing only refused saves) and each player version with Stored after the
  database save. A save still not stored after `--v2-resend-after` (10 s, as the game) is sent again (a resend),
  and so is everything unconfirmed right after a reconnect. At the end of the run the session keeps sending until everything is stored (the
  game's quit wait, `--v2-drain-timeout`), then the usual verification runs.
* **Events** are applied in sequence and acknowledged with the next save batch. The report counts gaps
  (missing sequence numbers, which would be lost events), repeats, and the events replayed after a resume.
* **Commands.** With `--v2-commands` joins, restores and the command mix go as `CommandRequest`s
  (additem `items.add`, produce `items.produce`, craft `items.craft`, equipall `equipment.equip-best`,
  highscore `highscore.rank`; `fetch` stays on REST). Each request has its own id; commands waiting during a
  drop go again with the same id after the reconnect. `--v2-repeat-share 0.1` sends 10% of them a second time
  with the same id and checks the answer is the same (status, value, inventory revision and amounts), and for
  `items.add` reads the player back over REST to check the item was not added twice.
* **Chaos.** `--v2-drop-every 1m` drops the socket without a close every minute, stays down for
  `--v2-drop-pause` (3 s), then resumes; the events the server made meanwhile must be replayed with no gap.

| v2 option | |
| --- | --- |
| `--protocol v1\|v2` | v1 (default) the delta and event sockets, v2 the WebSocket |
| `--v2-commands` | joins, restores and commands as v2 commands |
| `--v2-repeat-share <0..1>` | repeat this share of commands with the same request id (needs `--v2-commands`) |
| `--v2-drop-every <span>` | drop the WebSocket this often and resume |
| `--v2-drop-pause <t>` | stay down this long after a drop before resuming (default 3s) |
| `--v2-resend-after <t>` | send an unstored save again after this long without its Stored ack (default 10s, as the game's SaveOutbox) |
| `--v2-drain-timeout <t>` | how long to wait at the end for every save to be stored (default 30s) |
| `--first-session <n>` | number the sessions from n (streamer `<streamer><n>`, players `<prefix>_s<n>_...`), so two runs can share a server |
| `--server-pid <pid>` | report the server process's CPU time and memory (works for v1 too) |

The v2 numbers in the reports: player saves and resends, send to Accepted per batch and send to Stored per
player version (p50/p95/p99), players with unstored saves per tick, ack statuses, bytes and messages on the
socket (payload, without WebSocket and TLS framing), connects, drops, resumes, event gaps and replays,
command statuses and the repeat checks. The JSON report has them under `v2`, plus `bytesOnWire`,
`serverCpu` and `loadTesterCpu` for both protocols.

### Comparing v1 and v2

Run the same load on the same server, once per protocol, and compare the JSON files:

```powershell
$server = (Get-NetTCPConnection -LocalPort 443 -State Listen).OwningProcess
dotnet run -c Release --project src/RavenNest.LoadTest -- run --base-url https://localhost --players 1000 --duration 5m --command-rate 5 --verify-db --server-pid $server --json-report v1-1000.json
dotnet run -c Release --project src/RavenNest.LoadTest -- run --base-url https://localhost --players 1000 --duration 5m --command-rate 5 --verify-db --server-pid $server --json-report v2-1000.json --protocol v2 --v2-commands

# v2 with drops every minute and repeated commands
dotnet run -c Release --project src/RavenNest.LoadTest -- run --base-url https://localhost --players 1000 --duration 5m --command-rate 5 --verify-db --protocol v2 --v2-commands --v2-drop-every 1m --v2-repeat-share 0.1
```

Use `https://localhost` rather than `127.0.0.1` for a Release build of the server, which redirects anything
but localhost to www. What to compare: `bytesOnWire` (v1: delta and event socket payload; v2: the
WebSocket), `serverCpu.coresBusy`, the command endpoints (`GET players/...` against `v2 items.add` and so
on), events, and the verification. v1 has no save latency to compare: the delta socket never answers.

## Reading the results

* The delta socket gets no replies, so the client cannot time the server's handling of a frame. The
  server handles each frame inline on that connection's receive loop; when it falls behind, the socket
  buffers fill and the "socket write" times grow. Watch those, and the verification.
* `GET api/players/{id}` reads what the server holds in memory. Lost there means the server dropped the
  update. `--verify-db` shows what has been written to the database and how long that took.
* A player can be ahead of what was sent: the server's own task processors and items can add exp. Only
  behind counts.
* Run the tool on another machine (with `--i-know-this-is-not-production` and `--url` pointing at the
  test server) when the numbers need to be free of the tool competing with the server for CPU.

## Code layout

* `Delta/` the delta socket and frame writer (port of the game's `DeltaClient`).
* `V2/` the v2 WebSocket (`V2Connection`: Hello, one read loop, one write loop); `Simulation/V2Link.cs` is
  the game side on top of it (outbox, acks, events, commands, resume, chaos).
* `Events/` the game event socket.
* `Rest/` the REST calls, sent the way `WebApiRequest` sends them.
* `Simulation/` the streamer session, players, verification and roster.
* `Seed.cs` the seed script, `SelfTest.cs` the offline checks, `Report.cs` the console and JSON output.
