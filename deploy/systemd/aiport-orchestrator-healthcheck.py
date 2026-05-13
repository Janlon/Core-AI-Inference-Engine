#!/usr/bin/env python3

import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from urllib import error, request


HEALTH_URL = os.getenv("AIPORT_ORCHESTRATOR_HEALTH_URL", "http://127.0.0.1:5000/health")
SERVICE_NAME = os.getenv("AIPORT_ORCHESTRATOR_SERVICE_NAME", "aiport-orchestrator.service")
STATE_FILE = Path(os.getenv("AIPORT_ORCHESTRATOR_HEALTH_STATE_FILE", "/var/lib/aiport/orchestrator-healthcheck.json"))
FAILURE_THRESHOLD = max(1, int(os.getenv("AIPORT_ORCHESTRATOR_HEALTH_FAILURE_THRESHOLD", "3")))
TIMEOUT_SECONDS = max(1, int(os.getenv("AIPORT_ORCHESTRATOR_HEALTH_TIMEOUT_SECONDS", "5")))
RESTART_ON_DEGRADED = os.getenv("AIPORT_ORCHESTRATOR_RESTART_ON_DEGRADED", "false").strip().lower() in {"1", "true", "yes", "on"}


def utc_now_iso() -> str:
    return datetime.now(timezone.utc).isoformat()


def load_state() -> dict:
    if not STATE_FILE.exists():
        return {"consecutive_failures": 0}

    try:
        return json.loads(STATE_FILE.read_text(encoding="utf-8"))
    except Exception:
        return {"consecutive_failures": 0}


def save_state(state: dict) -> None:
    STATE_FILE.parent.mkdir(parents=True, exist_ok=True)
    STATE_FILE.write_text(json.dumps(state, ensure_ascii=True, indent=2), encoding="utf-8")


def acceptable_status(overall: str | None) -> bool:
    if overall == "healthy":
        return True

    if overall == "degraded" and not RESTART_ON_DEGRADED:
        return True

    return False


def fetch_health() -> tuple[str | None, str]:
    req = request.Request(HEALTH_URL, headers={"Accept": "application/json"})

    with request.urlopen(req, timeout=TIMEOUT_SECONDS) as response:
        body = response.read().decode("utf-8")
        payload = json.loads(body)
        overall = str(payload.get("overall") or "").strip().lower() or None
        return overall, body


def restart_service() -> tuple[bool, str]:
    result = subprocess.run(
        ["systemctl", "restart", SERVICE_NAME],
        capture_output=True,
        text=True,
        check=False,
    )
    output = (result.stdout or "") + (result.stderr or "")
    return result.returncode == 0, output.strip()


def main() -> int:
    state = load_state()
    consecutive_failures = int(state.get("consecutive_failures", 0))

    try:
        overall, _body = fetch_health()
        if acceptable_status(overall):
            state.update(
                {
                    "consecutive_failures": 0,
                    "last_checked_at": utc_now_iso(),
                    "last_overall": overall,
                    "last_action": "noop",
                }
            )
            save_state(state)
            print(f"Health OK for {SERVICE_NAME}: overall={overall}")
            return 0

        consecutive_failures += 1
        state.update(
            {
                "consecutive_failures": consecutive_failures,
                "last_checked_at": utc_now_iso(),
                "last_overall": overall,
                "last_action": "count_failure",
            }
        )
        save_state(state)
        print(
            f"Health degraded for {SERVICE_NAME}: overall={overall}, "
            f"consecutive_failures={consecutive_failures}/{FAILURE_THRESHOLD}"
        )
    except (error.URLError, TimeoutError, json.JSONDecodeError, OSError) as exc:
        consecutive_failures += 1
        state.update(
            {
                "consecutive_failures": consecutive_failures,
                "last_checked_at": utc_now_iso(),
                "last_overall": "unreachable",
                "last_error": str(exc),
                "last_action": "count_failure",
            }
        )
        save_state(state)
        print(
            f"Health probe failed for {SERVICE_NAME}: {exc}; "
            f"consecutive_failures={consecutive_failures}/{FAILURE_THRESHOLD}",
            file=sys.stderr,
        )

    if consecutive_failures < FAILURE_THRESHOLD:
        return 1

    restarted, output = restart_service()
    state.update(
        {
            "consecutive_failures": 0 if restarted else consecutive_failures,
            "last_checked_at": utc_now_iso(),
            "last_action": "restart" if restarted else "restart_failed",
            "last_restart_output": output,
        }
    )
    save_state(state)

    if restarted:
        print(f"Restarted {SERVICE_NAME} after {FAILURE_THRESHOLD} consecutive failures.")
        return 0

    print(f"Failed to restart {SERVICE_NAME}: {output}", file=sys.stderr)
    return 2


if __name__ == "__main__":
    raise SystemExit(main())