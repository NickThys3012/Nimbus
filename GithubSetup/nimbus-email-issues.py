#!/usr/bin/env python3
"""
nimbus-email-issues.py

Creates the transactional-email work items for Nimbus as GitHub issues, then
adds them to the "Nimbus Delivery" Projects v2 board and sets Sprint / Points /
Priority.

Provider decision encoded here: Brevo (free tier, 300 mails/day, EU-hosted).
Sending identity is the subdomain send.nickthys.be, never the apex, and never
the VPS itself.

Dry run by default. Nothing is written without --apply.

    python3 nimbus-email-issues.py                 # dry run, prints the plan
    python3 nimbus-email-issues.py --apply         # create issues + set fields
    python3 nimbus-email-issues.py --apply --skip-board   # issues only

Requires the gh CLI, authenticated, with the project scope:

    gh auth refresh -s project

Safe to re-run: issues are matched by exact title before creation, board items
are detected rather than duplicated, and setting a field to the value it
already holds is a no-op.
"""

import argparse
import json
import subprocess
import sys
import textwrap

REPO_DEFAULT = "NickThys3012/Nimbus"
PROJECT_TITLE = "Nimbus Delivery"

# Milestones are matched by prefix, case-insensitively, against what actually
# exists in the repo. "Sprint 0b" will find "Sprint 0b — Production readiness"
# regardless of how the em dash and suffix are written.
MILESTONE_PREFIXES = {
    "S0B": "sprint 0b",
    "S1": "sprint 1",
    "BL": "backlog",
}

# Board single-select values, matched case-insensitively against the real
# options. Adjust if your board uses different names.
SPRINT_VALUES = {
    "S0B": "Sprint 0b",
    "S1": "Sprint 1",
    "BL": "Backlog",
}

# ---------------------------------------------------------------------------
# The issues
# ---------------------------------------------------------------------------

ISSUES = [
    {
        "key": "MAIL-1",
        "title": "Mail: authenticate send.nickthys.be for outbound transactional mail",
        "milestone": "S0B",
        "points": 2,
        "priority": "High",
        "labels": ["infrastructure", "chore"],
        "body": """
        Nimbus sends account-approval, password-reset and share notifications.
        These are critical path: a mail that silently lands in spam is a pilot
        who cannot get into the app.

        Sending is done through Brevo, not from the VPS. Contabo IP ranges are
        widely blocklisted and Microsoft (Outlook/Hotmail/Live) blanket-rejects
        most VPS hosting ranges regardless of how correct SPF/DKIM/DMARC are.

        Sending identity is the subdomain `send.nickthys.be`, not the apex.
        This keeps application reputation isolated from personal mail on
        `nickthys.be`, so a bounce storm from Nimbus cannot affect the real
        mailbox.

        ## Records to publish

        | Type  | Host                                | Purpose                        |
        |-------|-------------------------------------|--------------------------------|
        | TXT   | `send.nickthys.be`                  | Brevo domain verification code |
        | TXT   | `send.nickthys.be`                  | SPF, `include:spf.brevo.com`   |
        | TXT   | `brevo._domainkey.send.nickthys.be` | DKIM public key                |
        | TXT   | `_dmarc.nickthys.be`                | DMARC policy, start at `p=none` |

        Exact values come from the Brevo dashboard — copy them verbatim rather
        than from any document.

        ## Acceptance criteria

        - [ ] `send.nickthys.be` shows as verified in Brevo
        - [ ] `dig TXT send.nickthys.be` returns exactly one SPF record
        - [ ] DKIM selector resolves and Brevo reports it valid
        - [ ] DMARC published at the apex with `p=none` and an `rua` address
        - [ ] Any pre-existing SPF record on the apex is left untouched
        - [ ] A test send passes SPF, DKIM and DMARC alignment at mail-tester.com

        ## Notes

        Two TXT SPF records on the same host is a permanent error, not a
        warning — the apex keeps its own record, the subdomain gets a separate
        one. No MX record is needed: Nimbus only sends.

        Setup guide: `docs/nimbus-email-setup.md`, Parts A–C.
        """,
    },
    {
        "key": "MAIL-2",
        "title": "Mail: provision Brevo account and wire SMTP credentials into deploy secrets",
        "milestone": "S0B",
        "points": 1,
        "priority": "High",
        "labels": ["infrastructure", "chore"],
        "body": """
        Brevo chosen on cost and data residency: free tier is 300 mails/day
        with no expiry, and it is French-hosted, which keeps pilot personal
        data in the EU without a transfer assessment.

        Volume sanity check: Nimbus sends on registration, approval, password
        reset and dossier share. Even at fifty active pilots that is a few
        dozen mails a week — three orders of magnitude under the free ceiling.

        ## Acceptance criteria

        - [ ] Brevo account created, sender `noreply@send.nickthys.be` added
        - [ ] SMTP key generated (this is not the account password)
        - [ ] `Email__SmtpUser` and `Email__SmtpPassword` added as GitHub Actions
              repository secrets
        - [ ] Same values written to `/opt/nimbus/.env` on the VPS, file mode 600
        - [ ] `.env` confirmed absent from git and present in `.gitignore`
        - [ ] Daily-send-limit alert configured in Brevo

        ## Notes

        The SMTP key is revocable independently of the account password —
        rotate it rather than the password if it leaks. Store nothing in
        `appsettings.json`; all six `Email__*` values come from environment.

        Setup guide: `docs/nimbus-email-setup.md`, Part D.
        """,
    },
    {
        "key": "MAIL-3",
        "title": "Mail: IEmailSender abstraction with MailKit SMTP implementation",
        "milestone": "S0B",
        "points": 3,
        "priority": "High",
        "labels": ["backend", "feature"],
        "body": """
        A provider-agnostic send path so swapping Brevo for Postmark later is a
        config change, not a code change.

        Deliberately **not** using a Postfix null-client relay container on the
        VPS. It was considered — it would let the API talk to `localhost:25` —
        but since host, port and credentials already come from environment
        variables, the relay buys nothing and adds a container to patch.

        ## Scope

        - `EmailMessage` record: to, subject, HTML body, plain-text body, optional reply-to
        - `IEmailSender` in `Nimbus.Application.Abstractions.Email`
        - `SmtpEmailSender` using MailKit, one `SmtpClient` per send (MailKit's
          client is not thread-safe and must not be a singleton)
        - `EmailOptions` bound from the `Email` config section with
          `ValidateDataAnnotations().ValidateOnStart()`
        - `NullEmailSender` that logs instead of sending, selected when
          `Email:Enabled` is false

        ## Acceptance criteria

        - [ ] Every message carries both an HTML and a plain-text alternative
              (HTML-only bodies score badly with spam filters)
        - [ ] `From` is `noreply@send.nickthys.be`, `Reply-To` is configurable
        - [ ] Missing or malformed config fails at startup, not at first send
        - [ ] Send outcome logged via Serilog with recipient, subject and
              provider message ID; failures logged at Error with the SMTP response
        - [ ] Unit tests cover option validation and the null-sender path

        ## Notes

        `SmtpEmailSender` returns a result rather than throwing on a rejected
        recipient — a bad address on one share notification must not fail the
        surrounding request.

        Setup guide: `docs/nimbus-email-setup.md`, Part E.
        """,
    },
    {
        "key": "MAIL-4",
        "title": "Mail: retry, failure handling and delivery logging",
        "milestone": "S0B",
        "points": 3,
        "priority": "Medium",
        "labels": ["backend", "feature"],
        "body": """
        Sending inline in the request thread means a slow SMTP handshake
        becomes a slow API response, and a transient provider blip becomes a
        lost password reset.

        ## Scope

        - Retry on transient SMTP failures (4xx, timeouts, connection reset)
          with exponential backoff; no retry on permanent 5xx rejections
        - Send off the request thread so the HTTP response is not blocked
        - A `SentEmail` audit row: recipient, template, sent-at, outcome,
          provider message ID, failure reason

        ## Acceptance criteria

        - [ ] Transient failure retried, permanent rejection not retried
        - [ ] A failed send never surfaces as a 500 to the caller
        - [ ] Failures visible in Grafana via the Loki sink, with a distinct
              log event name that can be alerted on
        - [ ] Audit row written for every attempt, success or failure

        ## Notes

        Explicitly avoiding fire-and-forget `Task.Run` here. FlightPrep used
        that for `LoginEvent` and flagged it in a comment as lossy on app
        shutdown — same trade-off, worse consequence, since a dropped mail is
        invisible to everyone including the user waiting for it.

        Migration must be additive: the `SentEmail` table is a new table only,
        no changes to existing columns, so rolling the image back leaves the
        previous release working.
        """,
    },
    {
        "key": "MAIL-5",
        "title": "Mail: Dutch HTML templates for the transactional set",
        "milestone": "S1",
        "points": 3,
        "priority": "Medium",
        "labels": ["backend", "feature"],
        "body": """
        Domain language is Belgian Dutch, consistent with the rest of Nimbus.

        ## Templates

        | Template          | Trigger                                    |
        |-------------------|--------------------------------------------|
        | `AccountPending`  | Registration accepted, awaiting approval   |
        | `AccountApproved` | Admin approves the account                 |
        | `AccountRejected` | Admin rejects the account                  |
        | `PasswordReset`   | Reset requested                            |
        | `DossierShared`   | A flight dossier is shared with a pilot    |

        ## Acceptance criteria

        - [ ] Every template renders both HTML and plain text from one source
        - [ ] Inline CSS only, table-based layout, max width 600px — mail
              clients do not support external stylesheets or modern layout
        - [ ] Renders correctly in Outlook desktop, Gmail web and iOS Mail
        - [ ] Dark-mode safe: no white-on-white after client colour inversion
        - [ ] No tracking pixels and no external image hosts
        - [ ] Token links expire and the expiry is stated in the body

        ## Notes

        Passengers cannot receive dossier share access, so `DossierShared` only
        ever addresses a registered pilot — no anonymous-recipient variant.
        """,
    },
    {
        "key": "MAIL-6",
        "title": "Mail: wire ASP.NET Identity approval and password reset to IEmailSender",
        "milestone": "S1",
        "points": 3,
        "priority": "High",
        "labels": ["backend", "feature"],
        "body": """
        Connects the send path to the account lifecycle. Depends on MAIL-3 and
        MAIL-5.

        ## Scope

        - Adapter from Identity's `IEmailSender<ApplicationUser>` to the Nimbus
          `IEmailSender`
        - Registration sends `AccountPending`
        - Admin approve/reject sends `AccountApproved` / `AccountRejected`
        - Password reset sends `PasswordReset` with a tokenised link

        ## Acceptance criteria

        - [ ] Reset endpoint returns the same response whether or not the
              address exists — no account enumeration through timing or wording
        - [ ] Reset link points at the Angular route, not an API endpoint
        - [ ] Approval mail is sent from the same transaction path that flips
              `IsApproved`, and a send failure does not roll back the approval
        - [ ] Reset tokens are single-use and time-limited
        - [ ] Integration test asserts the correct template fires per action

        ## Notes

        FlightPrep validated the password before the approval check
        specifically to prevent enumeration via the approval gate. Keep that
        property here: the reset flow must not become the enumeration oracle
        the login flow was hardened against.
        """,
    },
    {
        "key": "MAIL-7",
        "title": "Mail: Mailpit container for local development",
        "milestone": "S0B",
        "points": 2,
        "priority": "Medium",
        "labels": ["infrastructure", "chore"],
        "body": """
        Nobody should burn real Brevo quota, or real deliverability reputation,
        to check that a template renders.

        Mailpit rather than MailHog — MailHog is effectively unmaintained.

        ## Acceptance criteria

        - [ ] `axllent/mailpit` in the dev Compose profile, SMTP 1025, UI 8025
        - [ ] Dev config points `Email:SmtpHost` at `mailpit`, no auth, no TLS
        - [ ] Production Compose does not include the container
        - [ ] Every template viewable in the Mailpit UI, HTML and text parts
        - [ ] Documented in the README next to the other dev containers

        ## Notes

        Same principle as MinIO: one container definition, dev and production
        differ by profile and configuration rather than by a separate emulator.
        """,
    },
    {
        "key": "MAIL-8",
        "title": "Mail: ramp DMARC from p=none to p=quarantine",
        "milestone": "BL",
        "points": 1,
        "priority": "Low",
        "labels": ["infrastructure", "chore"],
        "body": """
        Follow-up to MAIL-1. Deliberately a separate item because it must not
        happen until aggregate reports prove alignment is clean.

        ## Acceptance criteria

        - [ ] At least two weeks of `rua` reports reviewed
        - [ ] All legitimate Nimbus mail passing both SPF and DKIM alignment
        - [ ] No unexpected sending sources in the reports
        - [ ] Policy moved to `p=quarantine`
        - [ ] Re-checked a week later before considering `p=reject`

        ## Notes

        Going to `p=reject` before DKIM alignment is verified is how people
        blackhole their own password-reset mail and cannot tell, because the
        rejection happens at the recipient's gateway and nothing appears in
        application logs.
        """,
    },
]

# ---------------------------------------------------------------------------
# gh plumbing
# ---------------------------------------------------------------------------


class GhError(RuntimeError):
    pass


def gh(args, parse_json=True, check=True):
    proc = subprocess.run(
        ["gh"] + args, capture_output=True, text=True
    )
    if proc.returncode != 0:
        if check:
            raise GhError(f"gh {' '.join(args)}\n{proc.stderr.strip()}")
        return None
    if not parse_json:
        return proc.stdout.strip()
    out = proc.stdout.strip()
    return json.loads(out) if out else None


def graphql(query, **variables):
    args = ["api", "graphql", "-f", f"query={query}"]
    for key, value in variables.items():
        flag = "-F" if isinstance(value, (int, float)) else "-f"
        args += [flag, f"{key}={value}"]
    return gh(args)


def require_gh():
    if subprocess.run(["which", "gh"], capture_output=True).returncode != 0:
        sys.exit("gh CLI not found on PATH.")
    if subprocess.run(
        ["gh", "auth", "status"], capture_output=True
    ).returncode != 0:
        sys.exit("gh is not authenticated. Run: gh auth login")


def body_of(issue):
    return textwrap.dedent(issue["body"]).strip() + "\n"


# ---------------------------------------------------------------------------
# Phase 1 — resolve repo state
# ---------------------------------------------------------------------------


def resolve_repo_state(repo):
    milestones = gh(["api", f"repos/{repo}/milestones?state=all&per_page=100"])
    labels = gh(["api", f"repos/{repo}/labels?per_page=100"])
    existing = gh(
        [
            "issue",
            "list",
            "--repo",
            repo,
            "--state",
            "all",
            "--limit",
            "500",
            "--json",
            "number,title",
        ]
    )
    return {
        "milestones": {m["title"]: m["number"] for m in milestones},
        "labels": {l["name"].lower() for l in labels},
        "issues": {i["title"]: i["number"] for i in existing},
    }


def match_milestone(state, key):
    prefix = MILESTONE_PREFIXES[key].lower()
    for title in state["milestones"]:
        if title.lower().startswith(prefix):
            return title
    return None


def usable_labels(state, wanted):
    keep, dropped = [], []
    for label in wanted:
        if label.lower() in state["labels"]:
            keep.append(label)
        else:
            dropped.append(label)
    return keep, dropped


# ---------------------------------------------------------------------------
# Phase 2 — create issues
# ---------------------------------------------------------------------------


def plan_issues(repo, state):
    plan = []
    for issue in ISSUES:
        milestone = match_milestone(state, issue["milestone"])
        labels, dropped = usable_labels(state, issue["labels"])
        plan.append(
            {
                "issue": issue,
                "existing": state["issues"].get(issue["title"]),
                "milestone": milestone,
                "labels": labels,
                "dropped_labels": dropped,
            }
        )
    return plan


def print_issue_plan(plan, state):
    print("=" * 78)
    print("PHASE 1 — issues")
    print("=" * 78)
    for entry in plan:
        issue = entry["issue"]
        if entry["existing"]:
            status = f"EXISTS  #{entry['existing']}"
        else:
            status = "CREATE"
        print(f"\n  [{status}] {issue['key']}  {issue['title']}")
        milestone = entry["milestone"] or (
            f"NOT FOUND (wanted prefix '{MILESTONE_PREFIXES[issue['milestone']]}')"
        )
        print(f"      milestone : {milestone}")
        print(f"      labels    : {', '.join(entry['labels']) or '(none)'}")
        if entry["dropped_labels"]:
            print(
                f"      dropped   : {', '.join(entry['dropped_labels'])}"
                "  <- not in repo, will be skipped"
            )
        print(
            f"      board     : {SPRINT_VALUES[issue['milestone']]}"
            f" / {issue['points']} pts / {issue['priority']}"
        )

    missing = [e for e in plan if e["milestone"] is None]
    if missing:
        print("\n  Milestones present in the repo:")
        for title in sorted(state["milestones"]):
            print(f"    - {title}")


def create_issues(repo, plan, apply):
    created = {}
    for entry in plan:
        issue = entry["issue"]
        if entry["existing"]:
            created[issue["key"]] = entry["existing"]
            continue
        if not apply:
            continue
        args = [
            "issue",
            "create",
            "--repo",
            repo,
            "--title",
            issue["title"],
            "--body",
            body_of(issue),
        ]
        for label in entry["labels"]:
            args += ["--label", label]
        if entry["milestone"]:
            args += ["--milestone", entry["milestone"]]
        try:
            url = gh(args, parse_json=False)
            number = int(url.rstrip("/").split("/")[-1])
            created[issue["key"]] = number
            print(f"  created #{number}  {issue['title']}\n           {url}")
        except GhError as exc:
            print(f"  FAILED  {issue['title']}\n          {exc}")
    return created


# ---------------------------------------------------------------------------
# Phase 3 — project board
# ---------------------------------------------------------------------------

PROJECT_QUERY = """
query($login: String!) {
  user(login: $login) {
    projectsV2(first: 30) {
      nodes {
        id
        number
        title
        fields(first: 40) {
          nodes {
            ... on ProjectV2FieldCommon { id name dataType }
            ... on ProjectV2SingleSelectField {
              id name options { id name }
            }
          }
        }
      }
    }
  }
}
"""

ITEMS_QUERY = """
query($project: ID!, $cursor: String) {
  node(id: $project) {
    ... on ProjectV2 {
      items(first: 100, after: $cursor) {
        pageInfo { hasNextPage endCursor }
        nodes {
          id
          content { ... on Issue { number } }
        }
      }
    }
  }
}
"""

ADD_ITEM = """
mutation($project: ID!, $content: ID!) {
  addProjectV2ItemById(input: {projectId: $project, contentId: $content}) {
    item { id }
  }
}
"""

SET_TEXT_OR_NUMBER = """
mutation($project: ID!, $item: ID!, $field: ID!, $value: Float!) {
  updateProjectV2ItemFieldValue(input: {
    projectId: $project, itemId: $item, fieldId: $field,
    value: {number: $value}
  }) { projectV2Item { id } }
}
"""

SET_SINGLE_SELECT = """
mutation($project: ID!, $item: ID!, $field: ID!, $option: String!) {
  updateProjectV2ItemFieldValue(input: {
    projectId: $project, itemId: $item, fieldId: $field,
    value: {singleSelectOptionId: $option}
  }) { projectV2Item { id } }
}
"""

FIELD_ALIASES = {
    "sprint": ["sprint", "iteration", "milestone"],
    "points": ["points", "story points", "estimate", "size"],
    "priority": ["priority", "prio"],
}


def find_project(login, project_number):
    data = graphql(PROJECT_QUERY, login=login)
    nodes = data["data"]["user"]["projectsV2"]["nodes"]
    if project_number:
        for node in nodes:
            if node["number"] == project_number:
                return node, nodes
        return None, nodes
    for node in nodes:
        if node["title"].strip().lower() == PROJECT_TITLE.lower():
            return node, nodes
    return None, nodes


def find_field(project, logical):
    names = FIELD_ALIASES[logical]
    fields = project["fields"]["nodes"]
    for candidate in names:
        for field in fields:
            if field.get("name", "").strip().lower() == candidate:
                return field
    return None


def find_option(field, wanted):
    for option in field.get("options", []) or []:
        if option["name"].strip().lower() == wanted.strip().lower():
            return option
    return None


def board_items(project_id):
    mapping = {}
    cursor = None
    while True:
        if cursor:
            data = graphql(ITEMS_QUERY, project=project_id, cursor=cursor)
        else:
            data = gh(
                [
                    "api",
                    "graphql",
                    "-f",
                    f"query={ITEMS_QUERY}",
                    "-f",
                    f"project={project_id}",
                ]
            )
        items = data["data"]["node"]["items"]
        for node in items["nodes"]:
            content = node.get("content") or {}
            if content.get("number"):
                mapping[content["number"]] = node["id"]
        if not items["pageInfo"]["hasNextPage"]:
            return mapping
        cursor = items["pageInfo"]["endCursor"]


def issue_node_id(repo, number):
    owner, name = repo.split("/")
    query = """
    query($owner: String!, $name: String!, $number: Int!) {
      repository(owner: $owner, name: $name) {
        issue(number: $number) { id }
      }
    }
    """
    data = graphql(query, owner=owner, name=name, number=number)
    return data["data"]["repository"]["issue"]["id"]


def run_board(repo, numbers, project_number, apply):
    print("\n" + "=" * 78)
    print("PHASE 2 — project board")
    print("=" * 78)

    login = repo.split("/")[0]
    project, all_projects = find_project(login, project_number)
    if not project:
        print(f"\n  Project '{PROJECT_TITLE}' not found. Available:")
        for node in all_projects:
            print(f"    #{node['number']}  {node['title']}")
        print("\n  Re-run with --project <number>.")
        return

    print(f"\n  Project: #{project['number']} {project['title']}")

    fields = {}
    for logical in ("sprint", "points", "priority"):
        field = find_field(project, logical)
        fields[logical] = field
        if field:
            print(f"    field {logical:<9}: {field['name']} ({field['dataType']})")
        else:
            print(f"    field {logical:<9}: NOT FOUND")

    if not any(fields.values()):
        print("\n  No usable fields. Board field names on this project:")
        for field in project["fields"]["nodes"]:
            print(f"    - {field.get('name')}")
        return

    existing_items = board_items(project["id"]) if apply else {}

    print()
    for issue in ISSUES:
        number = numbers.get(issue["key"])
        if not number:
            print(f"  [SKIP  ] {issue['key']}  issue not created yet")
            continue

        sprint_value = SPRINT_VALUES[issue["milestone"]]
        notes = []

        if fields["sprint"]:
            if not find_option(fields["sprint"], sprint_value):
                options = ", ".join(
                    o["name"] for o in fields["sprint"].get("options", []) or []
                )
                notes.append(f"no sprint option '{sprint_value}' (have: {options})")
        if fields["priority"]:
            if not find_option(fields["priority"], issue["priority"]):
                options = ", ".join(
                    o["name"] for o in fields["priority"].get("options", []) or []
                )
                notes.append(
                    f"no priority option '{issue['priority']}' (have: {options})"
                )

        label = "SET   " if apply else "WOULD "
        print(
            f"  [{label}] #{number:<5} {issue['key']:<7} "
            f"{sprint_value} / {issue['points']} pts / {issue['priority']}"
        )
        for note in notes:
            print(f"            ! {note}")

        if not apply:
            continue

        item_id = existing_items.get(number)
        if not item_id:
            content_id = issue_node_id(repo, number)
            data = graphql(ADD_ITEM, project=project["id"], content=content_id)
            item_id = data["data"]["addProjectV2ItemById"]["item"]["id"]
            existing_items[number] = item_id

        if fields["sprint"]:
            option = find_option(fields["sprint"], sprint_value)
            if option:
                graphql(
                    SET_SINGLE_SELECT,
                    project=project["id"],
                    item=item_id,
                    field=fields["sprint"]["id"],
                    option=option["id"],
                )
        if fields["priority"]:
            option = find_option(fields["priority"], issue["priority"])
            if option:
                graphql(
                    SET_SINGLE_SELECT,
                    project=project["id"],
                    item=item_id,
                    field=fields["priority"]["id"],
                    option=option["id"],
                )
        if fields["points"]:
            graphql(
                SET_TEXT_OR_NUMBER,
                project=project["id"],
                item=item_id,
                field=fields["points"]["id"],
                value=issue["points"],
            )


# ---------------------------------------------------------------------------


def main():
    parser = argparse.ArgumentParser(
        description="Create the Nimbus transactional-email issues and set board fields."
    )
    parser.add_argument("--repo", default=REPO_DEFAULT)
    parser.add_argument("--project", type=int, default=None)
    parser.add_argument("--apply", action="store_true")
    parser.add_argument("--skip-board", action="store_true")
    args = parser.parse_args()

    require_gh()

    total = sum(i["points"] for i in ISSUES)
    print(f"\nRepo: {args.repo}")
    print(f"Mode: {'APPLY' if args.apply else 'DRY RUN (nothing will be written)'}")
    print(f"{len(ISSUES)} issues, {total} points\n")

    state = resolve_repo_state(args.repo)
    plan = plan_issues(args.repo, state)
    print_issue_plan(plan, state)

    if args.apply:
        print("\n" + "-" * 78)
        numbers = create_issues(args.repo, plan, apply=True)
    else:
        numbers = {
            e["issue"]["key"]: e["existing"] for e in plan if e["existing"]
        }

    if not args.skip_board:
        run_board(args.repo, numbers, args.project, args.apply)

    if not args.apply:
        print("\n" + "-" * 78)
        print("Dry run complete. Nothing was written. Re-run with --apply.")
    print()


if __name__ == "__main__":
    main()
