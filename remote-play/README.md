# Remote couch play

FishBowl 1.27.0 includes a browser LiveKit client and a reference token service. Windows hosts share one chosen game window. Guests watch video/audio and can request keyboard controls or standard-gamepad buttons mapped to keyboard keys. The host must approve one guest in the browser, enable controls in FishBowl and focus the selected game. Linux can join through the browser; native Linux hosting/input injection is not implemented.

## Token service

Install Node.js 22 or newer and pnpm 11. In this directory run `pnpm install --frozen-lockfile --ignore-scripts`, then `pnpm test`. The pinned browser SDK is included in `client/livekit.js` with its Apache 2.0 license. The server SDK is installed from the lockfile. Do not commit `node_modules` or real environment values.

Configure the environment using `.env.example` as the list of required variables. Node does not load that file automatically: either set environment variables through the service manager or run `node --env-file=.env server.mjs` with a private local `.env` file. Generate a random host key, for example with `node -e "console.log(require('node:crypto').randomBytes(32).toString('base64url'))"`. Keep LiveKit API keys and secrets on the service; enter only the host key into the host browser when creating a session. FishBowl does not save this key.

Use `LIVEKIT_URL` from LiveKit Cloud or a self-hosted LiveKit server, together with its API key and secret. Bind the token service to localhost and put it behind an HTTPS reverse proxy. Set `FISHBOWL_PUBLIC_ORIGIN` to the exact public HTTPS origin, such as `https://play.example.com`. The proxy should serve `/` and all `/api/` routes from this service, use a valid certificate and enforce deployment-level request limits. The service does not trust forwarded IP headers. Multiple users behind one proxy share its conservative request limit. A shared or public deployment needs its own user authentication and per-account limits; this reference service is for a private trusted host.

Start with `pnpm start`. The default listener is `127.0.0.1:8787`. Restarting the service invalidates its in-memory invitations; room access already issued lasts until its token expires or the room is deleted in LiveKit. Tokens allow admission for five minutes; connected rooms/invitations expire after two hours and are deleted by the running service. Keep the service running for expiry enforcement. Stop sharing in the browser deletes the room and disconnects participants. If the service becomes unavailable, stop/delete the room through LiveKit administration; local FishBowl shutdown still releases input and removes its bridge.

## Host and guest

1. Start the game and configure keyboard controls inside the emulator.
2. In Play, choose Share session. The selected game stays inside Play, with its exact window preselected in a modeless dialog. Enter the HTTPS service address and open the client. Multiplayer > Start a session uses the same flow for an already running emulator. A child-hosted game switches to an owned viewport so its renderer remains a top-level window for capture; it keeps its place inside FishBowl.
3. In the browser enter the host key and create a session. Choose **Share game window** and select that same window. Entire-screen sharing is rejected. Browser/OS support determines whether window audio is available.
4. Send the invitation code privately to the guest. The guest opens the public token-service address, enters the invitation and chooses **Join session**. If browser autoplay blocks sound, choose **Enable sound**.
5. For shared controls, approve one guest below the host preview, enable controls in FishBowl and bring the selected game to the foreground. The guest enables **Send controls to the host**. Turn these options off to return to viewing only.
6. Choose **Disconnect** in the host browser to delete the room. Close or stop the FishBowl session to release controls locally. Closing only the guest disconnects that guest.

Keys: arrows, Z/X/A/S, Enter, left Shift, Escape and Tab. A standard gamepad maps its stick/d-pad and buttons to those keys. This is keyboard emulation, not a virtual Xbox controller or a replacement for native emulator netplay. Elevated games and games using input methods that ignore `SendInput` may not accept controls. No files, clipboard, shell commands or desktop shortcuts are shared. This build does not configure an end-to-end encryption key; transport security follows the LiveKit deployment.

## Validation and remaining live checks

Automated tests cover token signatures and grants, host authorization, invitation expiry/deletion, arbitrary-key rejection, mappings, embedded client resources, and local bridge authorization/foreground guards. Browser layout checks can run without LiveKit credentials. They do not prove real network media or an emulator's input support. Before relying on remote play, test two devices against the configured service: shared-window video and available audio, approve/revoke controls, game focus loss, controller unplug, browser disconnect, service outage and room expiry. Inspect the selected emulator's key mappings. Never send API secrets in a bug report.

Protocol details: [LiveKit JavaScript client](https://docs.livekit.io/reference/client-sdk-js/) and [server SDK](https://docs.livekit.io/reference/server-sdk-js/).
