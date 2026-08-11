# 2026-08-11 — Share Rider run config via `.run/`, not `.idea/`

**Decision:** Commit `.run/Secret Base.run.xml` for Rider; keep ignoring `.idea/`.

**Why:** JetBrains' recommended shared location is the solution `.run/` folder. That gives a one-click **Secret Base** Run/Debug config without committing personal Rider workspace state under `.idea/`.
