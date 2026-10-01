# FishBowl LiveKit Cloud token service

This small service generates short-lived LiveKit Cloud room tokens for FishBowl. It is a server-side component: `LIVEKIT_API_SECRET` must remain in the hosting environment and must never be copied into FishBowl or committed to Git.

Set the values from `.env.example` in the host's secret/environment settings, install the package dependencies, then deploy the service behind HTTPS. FishBowl stores only the resulting public `https://.../token` endpoint. It does not store the LiveKit Cloud API key or secret.

`POST /token` expects an `x-fishbowl-pairing` header plus an opaque `room_name` and `participant_identity` in its JSON body. It returns the standard LiveKit connection response: `server_url` and `participant_token`. Names, emails, or other personal information should not be used for either ID.

The endpoint starts invite-only with a pairing secret. Replace this simple shared pairing scheme with user authentication before allowing public access or selling a hosted multiplayer service.
