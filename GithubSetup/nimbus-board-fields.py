#!/usr/bin/env python3
"""
nimbus-board-fields.py

Adds the issues created by nimbus-vps-pivot.py to the "Nimbus Delivery" project
board and sets their Sprint, Points and Priority fields.

Run this AFTER nimbus-vps-pivot.py --apply.

  Dry run (default -- prints the plan, changes nothing):
      python3 nimbus-board-fields.py

  Apply:
      python3 nimbus-board-fields.py --apply

Requires the `project` scope, which `gh auth login` does NOT grant by default:

      gh auth refresh -s project --hostname github.com

Without it every project call fails with a permissions error. That is the single
most common reason this script does nothing.

HOW IT WORKS
  Projects v2 addresses everything by node ID, not by name, so the script has to
  resolve four things before it can write anything: the project ID, the field
  IDs, the single-select option IDs, and the item ID of each issue on the board.
  All of that is discovered at runtime and printed in the dry run, so a rename on
  the board surfaces as a clear error rather than a silent no-op.
"""

import argparse
import json
import subprocess
import sys

OWNER = "NickThys3012"
REPO = "NickThys3012/Nimbus"
PROJECT_TITLE = "Nimbus Delivery"

# Field names to look for, in preference order. Projects lets you rename these,
# so match generously and report what was actually found.
FIELD_CANDIDATES = {
    "sprint":   ["sprint", "iteration"],
    "points":   ["points", "story points", "estimate", "size"],
    "priority": ["priority", "prio"],
}

# The issues created by the pivot script, matched by exact title.
TARGETS = [
    ("Production Docker Compose stack behind Caddy with automatic TLS",              "Sprint 0", 5, "P0"),
    ("Self-hosted MinIO object store in the production stack",                       "Sprint 0", 3, "P0"),
    ("Multi-stage production image: Angular bundle inside the .NET runtime",         "Sprint 0", 3, "P0"),
    ("Health and readiness endpoints the deploy gate and monitor can rely on",       "Sprint 0", 2, "P0"),
    ("Configuration and secrets inventory for the VPS deployment",                   "Sprint 0", 2, "P0"),
    ("Nightly backup of database and object store, off-site and encrypted, with a rehearsed restore",
                                                                                     "Sprint 0", 5, "P0"),
    ("External uptime and certificate monitoring, independent of the host",          "Sprint 0", 2, "P1"),
    ("Transactional email through an external provider",                             "Sprint 1", 3, "P0"),
    ("DNS, reverse DNS and the production hostname",                                 "Sprint 0", 1, "P1"),
    ("RAM budget, container resource limits and restart policies",                   "Sprint 0", 3, "P1"),
    ("Operational runbook for the single-host deployment",                           "Backlog",  3, "P2"),
]


def gh(args, check=True):
    try:
        r = subprocess.run(["gh"] + args, capture_output=True, text=True, check=check)
        return r.stdout
    except FileNotFoundError:
        sys.exit("gh CLI not found on PATH.")
    except subprocess.CalledProcessError as e:
        err = (e.stderr or "").strip()
        if "scope" in err.lower() or "permission" in err.lower():
            sys.exit(f"\nPermissions error from gh:\n  {err[:300]}\n\n"
                     "Most likely the token lacks the 'project' scope. Fix with:\n"
                     "  gh auth refresh -s project --hostname github.com\n")
        return None


def gh_json(args):
    out = gh(args, check=False)
    if not out:
        return None
    try:
        return json.loads(out)
    except json.JSONDecodeError:
        return None


def pick_field(fields, kind):
    """Find a field by fuzzy name match, preferring earlier candidates."""
    for wanted in FIELD_CANDIDATES[kind]:
        for f in fields:
            if f.get("name", "").strip().lower() == wanted:
                return f
    for wanted in FIELD_CANDIDATES[kind]:
        for f in fields:
            if wanted in f.get("name", "").strip().lower():
                return f
    return None


def option_id(field, value):
    for opt in field.get("options", []) or []:
        if opt.get("name", "").strip().lower() == value.strip().lower():
            return opt["id"]
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--owner", default=OWNER)
    ap.add_argument("--project", default=PROJECT_TITLE,
                    help="project title, or a number to skip the lookup")
    ap.add_argument("--apply", action="store_true",
                    help="actually make the changes (default is a dry run)")
    args = ap.parse_args()

    print(f"\nNimbus board fields")
    print(f"Mode: {'APPLYING' if args.apply else 'DRY RUN — nothing will change'}\n")

    # --- resolve the project -------------------------------------------------
    if args.project.isdigit():
        number = int(args.project)
        projects = gh_json(["project", "list", "--owner", args.owner,
                            "--format", "json"]) or {}
        proj = next((p for p in projects.get("projects", [])
                     if p.get("number") == number), None)
    else:
        projects = gh_json(["project", "list", "--owner", args.owner,
                            "--format", "json"])
        if not projects:
            sys.exit("Could not list projects. Check `gh auth status` and the "
                     "'project' scope.")
        proj = next((p for p in projects.get("projects", [])
                     if p.get("title", "").strip().lower()
                     == args.project.strip().lower()), None)
        if not proj:
            print("Available projects:")
            for p in projects.get("projects", []):
                print(f"  #{p.get('number')}  {p.get('title')}")
            sys.exit(f"\nNo project titled {args.project!r}. Re-run with "
                     f"--project <number> or the exact title.")

    pnum, pid = proj["number"], proj["id"]
    print(f"Project: #{pnum} {proj.get('title')}  ({pid})")

    # --- resolve the fields --------------------------------------------------
    fdata = gh_json(["project", "field-list", str(pnum), "--owner", args.owner,
                     "--limit", "50", "--format", "json"])
    if not fdata:
        sys.exit("Could not read project fields.")
    fields = fdata.get("fields", [])

    resolved = {}
    for kind in ("sprint", "points", "priority"):
        f = pick_field(fields, kind)
        resolved[kind] = f
        print(f"  {kind:<9} -> " + (f"{f['name']!r} ({f.get('type','?')})"
                                    if f else "NOT FOUND"))
    if not any(resolved.values()):
        print("\n  Fields on this board: "
              + ", ".join(repr(f.get("name")) for f in fields))
        sys.exit("None of the expected fields were found — nothing to set.")

    # --- map issue titles to numbers ----------------------------------------
    issues = gh_json(["issue", "list", "--repo", REPO, "--state", "open",
                      "--limit", "300", "--json", "number,title,url"]) or []
    by_title = {i["title"].strip(): i for i in issues}

    # --- map issue numbers to existing board items ---------------------------
    idata = gh_json(["project", "item-list", str(pnum), "--owner", args.owner,
                     "--limit", "500", "--format", "json"]) or {}
    items_by_issue = {}
    for it in idata.get("items", []):
        c = it.get("content") or {}
        if c.get("number"):
            items_by_issue[c["number"]] = it["id"]

    print(f"\n{len(issues)} open issues, {len(items_by_issue)} already on the board\n")

    missing, done, failed = [], 0, 0

    for title, sprint, points, priority in TARGETS:
        issue = by_title.get(title.strip())
        if not issue:
            missing.append(title)
            print(f"  NOT FOUND  {title[:64]}")
            continue

        num = issue["number"]
        item_id = items_by_issue.get(num)
        label = f"#{num} {title[:52]}"

        if not item_id:
            if not args.apply:
                print(f"  ADD+SET    {label}  [{sprint} / {points}pt / {priority}]")
                continue
            added = gh_json(["project", "item-add", str(pnum), "--owner",
                             args.owner, "--url", issue["url"], "--format", "json"])
            item_id = (added or {}).get("id")
            if not item_id:
                # item-add output format varies by gh version; re-list to find it.
                relist = gh_json(["project", "item-list", str(pnum), "--owner",
                                  args.owner, "--limit", "500",
                                  "--format", "json"]) or {}
                for it in relist.get("items", []):
                    if (it.get("content") or {}).get("number") == num:
                        item_id = it["id"]
                        break
            if not item_id:
                print(f"  FAILED     {label} — could not add to board")
                failed += 1
                continue

        if not args.apply:
            print(f"  SET        {label}  [{sprint} / {points}pt / {priority}]")
            continue

        ok = True
        for kind, value in (("sprint", sprint), ("points", points),
                            ("priority", priority)):
            f = resolved[kind]
            if not f:
                continue
            base = ["project", "item-edit", "--id", item_id,
                    "--project-id", pid, "--field-id", f["id"]]
            ftype = (f.get("type") or "").lower()
            if kind == "points" or "number" in ftype:
                base += ["--number", str(value)]
            elif "single" in ftype or f.get("options"):
                oid = option_id(f, str(value))
                if not oid:
                    print(f"             ! no option {value!r} on field "
                          f"{f['name']!r}")
                    ok = False
                    continue
                base += ["--single-select-option-id", oid]
            else:
                base += ["--text", str(value)]
            if gh(base, check=False) is None:
                ok = False

        print(f"  {'SET       ' if ok else 'PARTIAL   '} {label}"
              f"  [{sprint} / {points}pt / {priority}]")
        done += 1 if ok else 0
        failed += 0 if ok else 1

    print(f"\n{done} set, {failed} with problems, {len(missing)} not found")
    if missing:
        print("\nNot found on the repo — did nimbus-vps-pivot.py --apply run?")
        for t in missing:
            print(f"  - {t}")
    if not args.apply:
        print("\nDry run complete. Re-run with --apply to make these changes.")
    print()


if __name__ == "__main__":
    sys.exit(main())
