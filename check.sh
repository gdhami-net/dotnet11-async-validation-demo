#!/usr/bin/env bash
# Runs the suite that backs the post's claims. Exits non-zero if any fail.
# Set DOTNET to the full path of an 11.0.100-rc.2 dotnet if it isn't the one on PATH.
set -euo pipefail
cd "$(dirname "$0")"

echo "dotnet11-async-validation-demo: checking the post's claims"
echo "  TakenEmailTests         a taken email is a 400 with the field error; Required and"
echo "                          EmailAddress fail without a database lookup; case is ignored"
echo "  RaceTests               two sign-ups held until both passed validation: two rows without"
echo "                          the unique index, a 500 with it, the same 400 once it is caught"
echo "  SharedDbContextTests    two async checks sharing the scoped DbContext throw once queries"
echo "                          take time; a context per check from IDbContextFactory works"
echo "  HowValidationRunsTests  async attributes run at the same time; attribute order; the"
echo "                          IAsyncValidatableObject version; an MVC controller calls IsValid"
echo

"${DOTNET:-dotnet}" test --logger "console;verbosity=normal"
