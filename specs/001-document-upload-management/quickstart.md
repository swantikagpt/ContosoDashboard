# Quickstart: Validating Document Upload and Management

Manual/exploratory validation script for this feature end-to-end, once implementation is
complete. See `data-model.md` for schema and `contracts/` for the service/endpoint contracts these
steps exercise.

## Prerequisites

- .NET 9 SDK installed (`dotnet --list-sdks`).
- ClamAV `clamd` running locally (research.md #1) — or, if unavailable, the no-op
  `IVirusScanner` implementation registered for local dev (confirm which is active in
  `appsettings.Development.json` before testing FR-007).
- Database migrated/recreated with the new `Document`, `DocumentShare`, and `DocumentActivityLog`
  tables (`dotnet ef database update`, or delete the local `ContosoDashboard.db` file and let
  `EnsureCreated()` rebuild it, consistent with how this repo already initializes its database).
- App running: `dotnet run` from `ContosoDashboard/`, then sign in at `/login` as one of the four
  seeded users (see README's Mock Login System table).

## Scenario walkthrough (maps to spec User Stories, in priority order)

1. **Upload (P1)** — Sign in as Ni Kang (Employee). Go to the new Documents area, upload a PDF
   under 25 MB with a title and category. Expect a success confirmation and the document appears
   in "My Documents". Then attempt a 30 MB file and a `.exe` file — both must be rejected with a
   clear, specific message (size vs. unsupported type).
2. **Browse & search (P2)** — With a few documents uploaded across categories, filter "My
   Documents" by category and confirm only matching rows show. Search by a keyword in a title;
   confirm results return quickly and never include documents you're not authorized to see (sign
   in as a second seeded user to confirm their private/unshared documents don't appear in your
   search). Open a project you belong to and confirm its Project Documents tab lists every
   document associated with that project.
3. **Manage (P3)** — As the uploader, edit a document's title/category and confirm it updates
   everywhere it's listed. Replace its file and confirm the same document record now serves the
   new content. Delete a document you own and confirm it disappears everywhere, including from any
   user it had been shared with. Sign in as Camille Nicole (Project Manager) and confirm she can
   delete a document on one of her projects even though she didn't upload it.
4. **Share & notify (P4)** — Share a document with a specific individual user; sign in as that
   user and confirm an in-app notification appeared and the document shows up under "Shared with
   Me". Share a document as a team share against a project; confirm every *current* member of
   that project can see it, then add a new member to the project and confirm they gain access
   without the share being re-created (research.md #6) — and confirm removing a member revokes
   their access immediately (FR-022).
5. **Task & dashboard integration (P5)** — Open a task, attach an existing document, and upload a
   new one directly from the task. Confirm both appear on the task and are associated with the
   task's project. Go to the dashboard home page and confirm the "Recent Documents" widget shows
   the 5 most recent uploads and a correct total count.
6. **Audit & compliance (P6)** — Sign in as the System Administrator. Confirm you can open any
   document in the system regardless of ownership/sharing. Open the document activity report and
   confirm upload/download/delete/share actions are all present with who performed them and when
   — including an entry for a document you deleted earlier in this walkthrough (the log entry
   should still show the document's title even though the document itself is gone, per
   data-model.md's `DocumentTitleSnapshot`).

## Authorization spot-checks (Constitution Principle III)

- Directly guess another user's document ID in the download endpoint URL while signed in as an
  unrelated user with no share/project relationship to it — expect `404`, not `403` (contracts/
  document-download-endpoint.md).
- As a Team Lead, confirm you can see project-associated documents from your team members, but
  **cannot** see a team member's "Personal Files" category upload that has no project association
  (spec Clarifications session, 2026-09-15).

## Performance spot-checks

- With ~500 documents accessible to one seeded user (script/seed as needed), confirm the document
  list and a search both return in well under 2 seconds (SC-007), and a document preview begins
  displaying within 3 seconds (SC-008).
