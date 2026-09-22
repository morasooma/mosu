#!/usr/bin/env bash
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
echo "Checking public security boundary in: $REPO_DIR"

FAILED=0

# 1. Platform & Anticheat exclusions
if [ -d "$REPO_DIR/osu.Game/Platform" ]; then
    echo "FAIL: osu.Game/Platform directory exists"
    FAILED=1
else
    echo "OK: osu.Game/Platform is excluded"
fi

if find "$REPO_DIR" -name "PlatformBootstrap*.cs" 2>/dev/null | grep -q .; then
    echo "FAIL: PlatformBootstrap files found"
    FAILED=1
else
    echo "OK: PlatformBootstrap is excluded"
fi

if find "$REPO_DIR" -name "RuntimeExtension*.cs" 2>/dev/null | grep -q .; then
    echo "FAIL: RuntimeExtension files found"
    FAILED=1
else
    echo "OK: RuntimeExtension is excluded"
fi

# 2. Score submission requests and legacy submission artifacts
for req in CreateSoloScoreRequest SubmitSoloScoreRequest CreateRoomScoreRequest SubmitRoomScoreRequest APIScoreToken; do
    if find "$REPO_DIR" -name "${req}.cs" 2>/dev/null | grep -q .; then
        echo "FAIL: Score submission request/token ${req}.cs found"
        FAILED=1
    else
        echo "OK: ${req}.cs is excluded"
    fi
done

if [ -f "$REPO_DIR/osu.Game/Online/Legacy/StableScoreSubmissionData.cs" ]; then
    echo "FAIL: StableScoreSubmissionData.cs exists in public repository"
    FAILED=1
else
    echo "OK: StableScoreSubmissionData.cs is excluded"
fi

if [ -f "$REPO_DIR/osu.Game/Online/Legacy/StableRijndael.cs" ]; then
    echo "FAIL: StableRijndael.cs exists in public repository"
    FAILED=1
else
    echo "OK: StableRijndael.cs is excluded"
fi

# 3. SoloScoreInfo / ScoreInfo anticheat fields
if grep -rn "APIClientStateSubmission" "$REPO_DIR/osu.Game/Online/API/Requests/Responses/SoloScoreInfo.cs" >/dev/null 2>&1; then
    echo "FAIL: APIClientStateSubmission found in SoloScoreInfo.cs"
    FAILED=1
else
    echo "OK: APIClientStateSubmission absent from SoloScoreInfo.cs"
fi

if grep -rn "APIClientStateSubmission" "$REPO_DIR/osu.Game/Scoring/ScoreInfo.cs" >/dev/null 2>&1; then
    echo "FAIL: APIClientStateSubmission found in ScoreInfo.cs"
    FAILED=1
else
    echo "OK: APIClientStateSubmission absent from ScoreInfo.cs"
fi

if grep -rn "APIClientStateChallenge" "$REPO_DIR/osu.Game" >/dev/null 2>&1; then
    echo "FAIL: APIClientStateChallenge found in codebase"
    FAILED=1
else
    echo "OK: APIClientStateChallenge absent from codebase"
fi

# 4. Banned endpoints and hardcoded credentials
if grep -rn "lazer-api.g0v0.top" "$REPO_DIR" --exclude="Check-PublicBoundary.sh" >/dev/null 2>&1; then
    echo "FAIL: Hardcoded g0v0 API URL found"
    FAILED=1
else
    echo "OK: No hardcoded g0v0 API URL found"
fi

if grep -rn "osu.osunolimits.dev" "$REPO_DIR" --exclude="Check-PublicBoundary.sh" >/dev/null 2>&1; then
    echo "FAIL: Hardcoded osuNoLimits URL found"
    FAILED=1
else
    echo "OK: No hardcoded osuNoLimits URL found"
fi

if grep -rn "G0V0_" "$REPO_DIR/osu.Game/Online/ServerProfileManager.cs" >/dev/null 2>&1; then
    echo "FAIL: G0V0 constants found in ServerProfileManager.cs"
    FAILED=1
else
    echo "OK: No G0V0 constants in ServerProfileManager.cs"
fi

if grep -rn "OSUNOLIMITS_" "$REPO_DIR/osu.Game/Online/ServerProfileManager.cs" >/dev/null 2>&1; then
    echo "FAIL: OSUNOLIMITS constants found in ServerProfileManager.cs"
    FAILED=1
else
    echo "OK: No OSUNOLIMITS constants in ServerProfileManager.cs"
fi

if grep -rn 'ClientSecret = "[^"]\+"' "$REPO_DIR/osu.Game/Online/ServerProfileManager.cs" 2>/dev/null | grep -vq 'Protected'; then
    echo "FAIL: Hardcoded client secret found in ServerProfileManager.cs"
    FAILED=1
else
    echo "OK: No hardcoded client secrets in ServerProfileManager.cs"
fi

# 5. Score submission disabled checks
if grep -q "throw new NotSupportedException(\"Score submission is disabled in the public build.\");" "$REPO_DIR/osu.Game/Online/Legacy/StableScoreSubmissionClient.cs" 2>/dev/null; then
    echo "OK: StableScoreSubmissionClient.SubmitAsync is stubbed with NotSupportedException"
else
    echo "FAIL: StableScoreSubmissionClient.SubmitAsync is not properly stubbed"
    FAILED=1
fi

# 6. Spectator gameplay-frame forwarding disabled in OnlineSpectatorClient
SPECTATOR_CLIENT="$REPO_DIR/osu.Game/Online/Spectator/OnlineSpectatorClient.cs"
if [ ! -f "$SPECTATOR_CLIENT" ]; then
    echo "FAIL: OnlineSpectatorClient.cs not found"
    FAILED=1
else
    # Detect both SendFrameData and SendFrameDataV2 string and name variants
    if grep -Eq 'SendFrameData(V2)?' "$SPECTATOR_CLIENT" 2>/dev/null; then
        echo "FAIL: OnlineSpectatorClient forwards frames via SendFrameData / SendFrameDataV2 variant"
        FAILED=1
    else
        echo "OK: No SendFrameData or SendFrameDataV2 variants found in OnlineSpectatorClient"
    fi

    # Positively verify that SendFramesInternal is a Task.CompletedTask no-op
    if grep -q "Task SendFramesInternal" "$SPECTATOR_CLIENT" 2>/dev/null && \
       grep -A 5 "Task SendFramesInternal" "$SPECTATOR_CLIENT" 2>/dev/null | grep -q "return Task.CompletedTask;"; then
        echo "OK: SendFramesInternal is positively verified as a Task.CompletedTask no-op"
    else
        echo "FAIL: OnlineSpectatorClient.SendFramesInternal is not a verified Task.CompletedTask no-op"
        FAILED=1
    fi
fi

# 7. Authentication credentials stub and score submission disabled
AUTH_FILE="$REPO_DIR/osu.Game/Online/MosuClientAuthentication.cs"
if [ ! -f "$AUTH_FILE" ]; then
    echo "FAIL: MosuClientAuthentication.cs not found"
    FAILED=1
else
    # HeaderValue must be empty
    if grep -Eq 'HeaderValue\s*=\s*@?""' "$AUTH_FILE" && ! grep -Eq 'HeaderValue\s*=\s*@?"[^"]+"' "$AUTH_FILE"; then
        echo "OK: HeaderValue is empty"
    else
        echo "FAIL: MosuClientAuthentication.cs HeaderValue is not empty"
        FAILED=1
    fi

    # OAuthClientId must be empty
    if grep -Eq 'OAuthClientId\s*=\s*@?""' "$AUTH_FILE" && ! grep -Eq 'OAuthClientId\s*=\s*@?"[^"]+"' "$AUTH_FILE"; then
        echo "OK: OAuthClientId is empty"
    else
        echo "FAIL: MosuClientAuthentication.cs OAuthClientId is not empty"
        FAILED=1
    fi

    # OAuthClientSecret must be empty
    if grep -Eq 'OAuthClientSecret\s*=\s*@?""' "$AUTH_FILE" && ! grep -Eq 'OAuthClientSecret\s*=\s*@?"[^"]+"' "$AUTH_FILE"; then
        echo "OK: OAuthClientSecret is empty"
    else
        echo "FAIL: MosuClientAuthentication.cs OAuthClientSecret is not empty"
        FAILED=1
    fi

    # ScoreSubmissionEnabled must be exactly false
    if grep -Eq 'ScoreSubmissionEnabled\s*(=>|=)\s*false;' "$AUTH_FILE" && ! grep -Eq 'ScoreSubmissionEnabled\s*(=>|=)\s*true;' "$AUTH_FILE"; then
        echo "OK: ScoreSubmissionEnabled is exactly false"
    else
        echo "FAIL: MosuClientAuthentication.cs ScoreSubmissionEnabled is not exactly false"
        FAILED=1
    fi
fi

# 8. Offline SubmittingPlayer
if grep -q "OnlineRecordSendingDisabled => true" "$REPO_DIR/osu.Game/Screens/Play/SubmittingPlayer.cs" 2>/dev/null; then
    echo "OK: SubmittingPlayer.cs has offline notice and disabled online record sending"
else
    echo "FAIL: SubmittingPlayer.cs missing offline notice"
    FAILED=1
fi

# 9. SoloPlayer and RoomSubmittingPlayer submission overrides
if grep -q "CreateSubmissionRequest" "$REPO_DIR/osu.Game/Screens/Play/SoloPlayer.cs" 2>/dev/null; then
    echo "FAIL: SoloPlayer.cs overrides CreateSubmissionRequest"
    FAILED=1
else
    echo "OK: SoloPlayer.cs does not override CreateSubmissionRequest"
fi

if grep -q "CreateTokenRequest" "$REPO_DIR/osu.Game/Screens/Play/RoomSubmittingPlayer.cs" 2>/dev/null; then
    echo "FAIL: RoomSubmittingPlayer.cs overrides CreateTokenRequest"
    FAILED=1
else
    echo "OK: RoomSubmittingPlayer.cs does not override CreateTokenRequest"
fi

# 10. Required public boundary documentation
if [[ -e "$REPO_DIR/DODGE_IMPLEMENTATION_STATUS.md" ]]; then
    echo "FAIL: Private implementation status document is present: DODGE_IMPLEMENTATION_STATUS.md"
    FAILED=1
else
    echo "OK: Private implementation status document is absent"
fi

for doc in PUBLIC_SOURCE.md NOTICE.md README.md; do
    if [ -f "$REPO_DIR/$doc" ]; then
        echo "OK: $doc exists"
    else
        echo "FAIL: $doc missing"
        FAILED=1
    fi
done

# 11. Secret and signing files check (pfx, key, keystore, jks, snk, cer, crt, pem, and .env files)
# Excludes .git, bin, and obj build output directories safely
SECRET_FILES=$(find "$REPO_DIR" \
    -type d \( -name ".git" -o -name "bin" -o -name "obj" -o -name "TestResults" \) -prune -o \
    -type f \( \
        -name "*.pfx" -o \
        -name "*.key" -o \
        -name "*.keystore" -o \
        -name "*.jks" -o \
        -name "*.snk" -o \
        -name "*.cer" -o \
        -name "*.crt" -o \
        -name "*.pem" -o \
        -name ".env" -o \
        -name ".env.*" -o \
        -name "*.env" \
    \) -print 2>/dev/null || true)

if [ -n "$SECRET_FILES" ]; then
    echo "FAIL: Secret/signing or .env files detected:"
    while IFS= read -r f; do
        [ -n "$f" ] && echo "  - ${f#$REPO_DIR/}"
    done <<< "$SECRET_FILES"
    FAILED=1
else
    echo "OK: No secret/signing or .env files found"
fi

# 12. Tracked compiled binaries check (dll, so, exe, dylib, pdb, nupkg)
TRACKED_BINARIES=$(git -C "$REPO_DIR" ls-files 2>/dev/null | grep -Ei '\.(dll|so|exe|dylib|pdb|nupkg)$' || true)
if [ -n "$TRACKED_BINARIES" ]; then
    echo "FAIL: Tracked compiled binaries detected:"
    while IFS= read -r f; do
        [ -n "$f" ] && echo "  - $f"
    done <<< "$TRACKED_BINARIES"
    FAILED=1
else
    echo "OK: No tracked compiled binaries found"
fi

# 13. Unignored untracked bin/obj or binary outputs check
UNIGNORED_OUTPUTS=$(git -C "$REPO_DIR" ls-files --others --exclude-standard 2>/dev/null | grep -Ei '(^|/)(bin|obj)/|\.(dll|so|exe|dylib|pdb|nupkg)$' || true)
if [ -z "$UNIGNORED_OUTPUTS" ]; then
    UNIGNORED_OUTPUTS=$(git -C "$REPO_DIR" status --porcelain 2>/dev/null | grep -Ei '^\?\?\s.*(^|/)(bin|obj)/' || true)
fi

if [ -n "$UNIGNORED_OUTPUTS" ]; then
    echo "FAIL: Unignored untracked bin/obj or binary outputs detected:"
    while IFS= read -r f; do
        [ -n "$f" ] && echo "  - $f"
    done <<< "$UNIGNORED_OUTPUTS"
    FAILED=1
else
    echo "OK: No unignored untracked bin/obj or binary outputs found"
fi

if [ $FAILED -ne 0 ]; then
    echo "Boundary verification FAILED!"
    exit 1
fi

echo "Boundary verification PASSED!"
