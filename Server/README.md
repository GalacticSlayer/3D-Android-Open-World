# Fishing guest multiplayer

`MENU / TRAVEL → MULTIPLAYER` now offers **Open Server**, **Join Server**, an
4-character room code, **Copy Room Code**, and **Leave Server**. Opening creates a
room on Oracle; it does not host a process on the player's device. Up to eight
players per room can see animated, interpolated avatars walking/swimming around
the fishing islands. Players keep their existing world positions when joining:
travel to the same island to meet. Offline play makes no network requests.

## Scope

This first version is shared player presence, not full server-authoritative game
simulation. The server owns room membership, tokens, capacity and lifecycle,
validates message shape/world bounds, and relays client-reported positions. Local
movement physics stays on each device. Boats are shared with owner-simulated physics
and server-validated boarding/seat claims. Fish, catches, ripples, inventories and
economy are not synchronized through room snapshots. Local player saves remain
device-owned. Published aquarium layouts and fish collections use a separate,
persisted, read-only Home directory API (details below), not cloud saves.
Other avatars are hidden in the shop and unrelated Homes; room members visiting
the same Home can see each other. There is no player collision.

The host must stay connected. Leaving as host closes the room. A disconnected
host expires after 30 seconds. Transient request failures retry; after three
failures the game returns to offline and players can open/join again. Backend
restarts clear rooms; open a new room afterward. Android backgrounding can expire
a session. Protocol and Application.version must match on both devices; install
the same newly built game on both devices for testing.

## Shared boats

Placed boats appear for every player in the same room on the fishing islands.
Players can step aboard or use BOARD BOAT near the deck entrance, then use the
existing ROW BOAT / DRIVE BOAT control. Capacity limits still apply. On a rowboat,
the owner uses the front seat and the guest uses the rear seat; leaving the front
seat never promotes the guest. Each player's movement input drives their own oars.
The front rower steers, the rear rower adds propulsion, and both rowing together
use the existing two-rower speed. The motorboat (saved as `sailboat`) retains one helm and adds a non-driving
right-chair seat. With the helm occupied, a nearby player uses SIT / STAND UP.
Seat 2 carries the passenger animation but always has zero steering/throttle;
rowboat seat 2 retains its existing rowing behavior. Only one player may hold
each chair, and passengers are not promoted when the driver leaves. Install the
updated server runtime as well as rebuilding both clients for this feature.
The yacht retains one helm and walking decks.

The boat owner simulates hull physics and sends the transform with their normal
player snapshots. Guests use solid interpolated copies and send rowing/helm input
through the same connection. Avatars follow boat-local positions. Boat placement,
purchases and saves remain owner-only; occupied boats cannot be repositioned.
Removing a boat, traveling out of the fishing world, or disconnecting removes its
shared copy and releases passengers. Stale input stops after one second; a boat
whose owner stops responding disappears after three seconds.

After rebuilding both clients, place a rowboat, board with both players, and test
front-only, rear-only and simultaneous rowing. Confirm the guest cannot claim the
front seat, each oar pair stops with its own input, and both avatars stay on the
moving hull. Release seats, walk/fish on the deck, leave/reboard, and disconnect
the owner. Repeat guest helm use with a sailboat/yacht and check the raft's
single-passenger limit. Also confirm offline boating still works.

## Fishing tournaments

A tournament booth is added to the right side of the Suncrest marina, viewed from
land toward the pier. The host visits it, chooses an unlocked island and a wager
of 1–1,000,000 in-game coins, then invites the current room. Other players receive
a Join/Decline prompt anywhere in the world. Players without that island unlocked
cannot enter; unanswered invitations decline after 45 seconds. At least two
entrants are required. The host can cancel during invitations for full refunds.

After all decisions, the server randomly chooses one species that can actually
spawn in that island's tuning tables. Everyone sees the target for five seconds,
then the server runs a six-minute contest. Only successfully landed fish from new
casts in the selected island's waters count. A full bag/released fish, pond catch,
wrong species, old cast, or duplicate report cannot score. Points are summed at
100 per kg (each catch rounded to a whole point). The winner takes the entire
pool. Ties use biggest individual catch, then earliest attainment of the tied
score, then joining order for an exact simultaneous tie. No qualifying catches
means all entrants are refunded. Results show each entrant's points, the winner,
and the biggest qualifying catch in kilograms; the top-right × closes the panel.

Deep Ocean is now a selectable classic target-species tournament biome.
Players fish there by boat as usual; Deep Ocean has no island teleport marker.

The booth also offers **map-wide total-weight tournaments**. Every joined player
is automatically teleported to the Suncrest dock during the five-second countdown.
All eligible fish caught in any of the five fishing biomes count toward the same
total, using that biome's species list and maximum legal fish weight. The six-minute
winner is the entrant with the greatest combined catch weight (kg). Ties use biggest individual
fish, earliest score and joining order. The results leaderboard shows each
player's total kg, catch count, payout and the overall biggest fish with its
species. Click any player's name/row after a tournament to request their
individual catches and weights, in both formats. Detailed histories are kept on
the server and requested on demand; 10 Hz room snapshots stay compact.

During the map-wide total-weight countdown and running phase, all joined
competitors still have fast travel restricted to **Suncrest Reef/Beach and Tideglass
Quay (the shop)**. Home, Brinebreak, Bluewater, Snapper, and direct boat
teleports are blocked in the central travel controller as well as visually
disabled in the travel menus. The restriction lifts after the tournament
ends/cancels or the player disconnects; other tournament formats and players
not joined in this match keep their normal travel choices. The shop can be
visited and exited normally, with return travel to Suncrest. This is a
fast-travel restriction, not a physical barrier to sailing or swimming, and
valid catches from any biome count even when reached without fast travel.

Tournament weight validation permits the generated biome hotspot limit
plus up to +100% Charm, so legitimate record catches are not rejected.
The 120-catch limit and all existing duplicate/old-cast/species checks remain.

The local shop save atomically stores the coin debit and its recovery ticket.
The server persists escrow receipts in `DATA_DIR/tournament-escrow.json` using
atomic file replacement. Repeated requests cannot charge or score twice. Pending
entries are refunded on a server restart or host disconnect. Guests leaving
before play starts are refunded; after play starts their existing score and stake
remain, and winnings can still be recovered. Completed payouts survive restarts.
Clients retry unresolved receipts every five seconds, including after restarting
the game. Never delete the server escrow file or a player's pending wager records
while money remains unresolved. No new account/cloud-save system is introduced:
unlocks, balances and catches still originate in the existing client-owned save
and simulation; this is not a tamper-proof competitive economy.

Normal tournament state rides on existing room snapshots. Extra requests occur
only for decisions/catches and unresolved-receipt recovery; there is no server-side
fish simulation. `tournament-species.mjs` is generated from the current
`BiomeFishWeights.csv` and `BiomeBaitSpeciesChance.csv` (including the 50% hotspot
weight allowance; validation separately includes maximum Charm). Regenerate that list when fish availability/tuning changes.
The existing deployment service's StateDirectory provides the writable DATA_DIR.

Validate with `node --test Server/tests/*.test.mjs`. In Unity, build both clients,
check booth placement/access, locked-island prompts, wager debits, target reveal,
countdown, six-minute HUD, catch scoring, final leaderboard and close button.
Test host/guest disconnects and restart with an unsettled entry. Remote bait
fishing now displays only the line endpoint; retrieval lures remain visible.
Unity compilation and device playtesting are separate from the backend tests.

## One-time network activation on the existing Oracle VM

The private deployment health service remains on `127.0.0.1:3000`. Game traffic
uses TCP **3001** and is off until explicitly enabled. Download
`Server/ops/enable-multiplayer.sh` from the reviewed commit and run it with sudo.
It fetches the latest runtime through the existing updater, enables the game
listener with a systemd override, restarts the fishing service, and opens TCP 3001
in the active VM firewall zone. It stops with an explanation if firewalld is not
active. It does not change the tank VM.

In Oracle Console, open **fishing-game-vcn → fishing-game-public-subnet → its
Security List → Add Ingress Rules**. Add a stateful rule (Stateless unchecked):
source CIDR `0.0.0.0/0`, IP protocol TCP, source port All, destination port `3001`.
Do not replace existing SSH rules. No router forwarding or player private keys
are needed. From another machine, check `http://129.153.55.216:3001/health`.

The initial guest-only endpoint is plain HTTP, not encrypted. Use pseudonyms;
do not add account credentials or private saved data to this protocol. Session
tokens are ephemeral and may be intercepted on an untrusted network. Before
accounts or public production release, add HTTPS and change the client endpoint.
The editor/build helper enables HTTP and Android internet permission for this
prototype; remove the HTTP allowance when moving to HTTPS. This implementation
uses UnityWebRequest on Android/desktop with no third-party networking package.
WebGL is not supported by this initial setup (no browser CORS/mixed-content setup).

## Build and test

Pull `main` into Unity, let scripts finish compiling, then Build & Run on both
devices (or use Editor play mode for one player). No scene editing is needed.
Open MENU / TRAVEL, select MULTIPLAYER, enter a guest name, and choose Open Server.
On the other device, enter that code and choose Join Server. Close the menu and
walk/jump/swim: the other avatar should follow with interpolation. Travel to
another island and confirm both clients agree on where the avatar is. Travel to
Home and confirm the avatar hides; return to the fishing world to show it again.
Check Leave Server, an invalid code, and a disconnected network. Confirm offline
movement and fishing continue independently afterward.

Automated backend tests: `node --test Server/tests/*.test.mjs` (Node 24).
These cover actual HTTP open/join/movement exchange, room isolation, token
rejection, invalid/version-mismatched requests, capacity, host/guest leave,
expiry, and attempt limiting. Unity compilation, Android permissions/UI, and
real two-device visual testing still require Unity/devices; Node tests cannot
validate those. No live Oracle test is claimed until the one-time setup runs.

## Automatic deployment

Push deployable code under `Server/runtime/` to `main`. The updater checks about
every five minutes, shallow-fetching only this folder's blobs. Unity/ops-only
changes do not restart the service. Entry point: `server.mjs`. Keep the private
GET `/health` response `{ "ok": true, "release": process.env.RELEASE_ID }`.
If dependencies are added, commit both package.json and package-lock.json.
Production dependencies install with lifecycle scripts disabled.

Updates stage separately, validate entrypoint syntax, switch releases and restart.
A failed health check restores the last successful release and suppresses that
failed runtime until code changes. Interrupted initial installs can be retried.
The service restarts after crashes/reboots. Runtime updates do not update Android
builds, OS/Node, or ops scripts. Releases are retained; monitor disk as they grow.

Runtime user: `fishing-server`; deployment user: `fishing-deploy`. Runtime code
is read-only to the game process. Future writable data belongs in
`DATA_DIR=/var/lib/fishing-server`. Coordinator metadata uses
`/var/lib/fishing-deployment`, persistently labeled `init_var_lib_t` on SELinux.
The existing pinned `Server/ops/install.sh` handles first installation/ops updates.

```sh
curl http://127.0.0.1:3000/health
curl http://127.0.0.1:3001/health
systemctl status fishing-server fishing-update.timer --no-pager
sudo journalctl -u fishing-server -u fishing-update -n 80 --no-pager
sudo systemctl start fishing-update
```

Pause deployment: `sudo systemctl stop fishing-update.timer`.
Retry an unchanged failed version after fixing its environment:
`sudo rm /var/lib/fishing-deployment/failed-release`, then start fishing-update.



## Persistent public Home aquarium directory

MENU / TRAVEL -> MULTIPLAYER -> **VIEW AQUARIUMS** shows up to 20 public
Homes per page. Search by player name or eight-character Home code, sort by last
updated, number of resident fish or biggest exhibited fish, and visit without
opening a multiplayer room. Homes default to **Public**. Owners may set
**Unlisted** (code-only visitor access) or **Private** (not visitable by others).
The owner receives a random Home code and a private owner credential saved
separately in device PlayerPrefs. This is a device-bound prototype identity:
clearing app data or reinstalling without preserving PlayerPrefs loses ownership
of that published Home. It is not a cross-device account or cloud backup.

Only individual aquarium identity, type, local placement, rotation and resident
species/biome/weight, plus the owned Home pond tier and its residents, are
published. A missing pond restores the solid garden plot during visits.
Fish bags, coins, gear, progress, movement
and full save files are not uploaded. Owners' changes upload after a 2.5-second
debounce and retry after interruptions. An unchanged Home does not upload again,
even if other game saves happen. Visitors download a snapshot once, then check
only the small Home version every ten seconds; they download a new snapshot
only when it changes. Fish animation and water physics remain local to the
visitor's Unity device. Distant resident visuals are disabled just as in
the owner's Home.

Visitors cannot place, store, add, remove or sell fish from another player's
aquariums. They may inspect resident species and weights, swim in supported
aquariums, and click **RETURN TO MY HOME**. Normal multiplayer-room avatars
appear only if both peers occupy the exact same published Home code. Visiting
does not grant write permission or alter the visitor's original local aquarium
save. If an owner changes their Home to Private, visitors leave after the next
version check.

Server API on existing TCP 3001:
- POST /v1/homes/register  (new owner code + private credential)
- POST /v1/homes/update    (owner-only atomic snapshot write, max 256 KiB)
- POST /v1/homes/list      (public summaries, 20/page)
- POST /v1/homes/get       (full snapshot on demand, public/unlisted)
- POST /v1/homes/check     (lightweight revision check)
- POST /v1/homes/presence  (transient same-Home avatar poses and tank drag,
                            no fishing-room session required)

Active players sharing the same Home code are visible to one another without
opening a fishing room. Home player poses use the existing multiplayer
avatar format, without depending on aquarium movement. Active Homes poll at
approximately four times per second; an empty Home polls less frequently.
The server only keeps temporary Home-presence records in RAM; a nine-second
timeout removes disconnected players. A live owner drag sends a tiny temporary
aquarium position to visitors. A successful placement also sends a short-lived
`placed` announcement. Visitors pin the final aquarium position (and restore
its colliders and fish animation) until the disk-backed Home snapshot confirms
the position. A canceled drag has no `placed` announcement and reverts.
Owners publish permanent aquarium positions and fish additions/removals through
the same atomic Home snapshot endpoint, so revisiting or restarting the server
does not reset them. Only authenticated owners can broadcast moves or placements.
Fish still animate entirely on each phone; Home presence never transfers fish
positions, physics, inventory, or ownership.
Player visibility is strictly scoped to matching Home codes; fishing-island
multiplayer remains unchanged.

Directory metadata is kept in memory; full tank and fish snapshots are read
from disk only on a visit/update. Snapshots live in DATA_DIR/homes, separate
from multiplayer sessions and tournament-escrow.json. Do not delete that
directory when upgrading or restarting the Oracle VM. The existing 4 KiB
limit is retained for normal 10 Hz movement requests; only owner snapshot
uploads permit up to 256 KiB. New registrations are limited per IP and the
directory has a bounded profile count.

**Security note:** The existing multiplayer listener is still plain HTTP.
Owner credentials must not be considered protected against network sniffing
until HTTPS/TLS termination is configured and the Unity endpoint is updated.
Do not publish sensitive personal information as a player name. Public
listings are opt-out by design, per game requirements.

Regression tests:
\`node --test Server/tests/*.test.mjs\`
A GitHub Actions job also runs the Node 24 server tests on relevant pushes.
Unity compilation, editor Play Mode, Android build/run, and live Oracle
verification require the Unity editor, connected devices, and VM access.
