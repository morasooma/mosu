# Public source and official releases

The private Mosu repository remains the canonical source for official binaries.
This directory is a periodically exported public snapshot and may lag behind a
released binary.

## Score acceptance boundary

The public snapshot deliberately contains no production client key, OAuth
credential, platform seal or score-proof implementation. It sets
`MosuClientAuthentication.ScoreSubmissionEnabled` to `false`, contains no
score-token or score-submission request implementation, and records replays
locally without forwarding gameplay frames to the server.

These client-side restrictions only prevent accidental submission. They must
never be treated as authentication because anyone can edit and rebuild public
code.

The production server is responsible for rejecting score creation and score
submission whenever the request does not carry a valid, current proof issued to
an authorised private Mosu release. This requirement must cover solo, room,
multiplayer, legacy and replay-related submission paths. A generic API header,
client version string or client-controlled build flag is not sufficient proof.
