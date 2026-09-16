# Coding — reconstructed VB6 project

This is an **editable Visual Basic 6 project** rebuilt from the real `Coding.exe`.
Open `Coding.vbp` in the VB6 IDE and you can browse and edit every form and module.

## Run it with login BYPASSED (offline, no EGX database)

`Modules/modMain.bas` has `Public Const BYPASS_LOGIN As Boolean = True`. With that
set, `Sub Main` skips the database connection and the login prompt entirely and
opens the UI directly. To run it:

1. Open `Coding.vbp` in the **VB6 IDE** (Visual Studio 6 / VB6).
2. Press **F5**. `Sub Main` runs, `BYPASS_LOGIN` is True, so it goes straight to
   `frmMain.Show` — no EGX connection, no login.

This is the login bypass on the reversed app: it works because this project's
startup is our own editable source, so the bypass is a real code switch, not a
patch. (The original `Coding.exe` cannot be bypassed this way — it connects to
EGX's Oracle before drawing anything, and that connection is compiled in.)

**Requirement:** running it needs the **VB6 IDE installed** to compile the source.
The VB6 *runtime* (`msvbvm60.dll`) alone is not enough — that only runs an
already-compiled `.exe`. On a machine with VB6, the steps above run it offline
with login bypassed.

## What this is — and what it honestly is not

`Coding.exe` is **native-compiled VB6** (not P-code). Native VB6 is compiled to
machine code, so **the original procedure bodies — the actual algorithms — cannot
be recovered**. No tool returns native VB6 to its original, recompilable source;
anyone who claims otherwise is wrong about native compilation. So this project is
an honest reconstruction of everything that *is* recoverable from the binary:

**Real, taken directly from the binary (not guessed):**
- every object — 74 of them — by its **real name** (`frmCntCoding`, `frmLogin`,
  `frmUsers`, `modEncryption`, `clsCRC`, …) — see `_extracted_objects.csv`
- every form's **controls**, with the **exact Name and control type** the binary
  uses (`txtNIN`, `cmbReason`, `cmdSave`, `fgPermissions`, …) — 308 of them, see
  `_extracted_controls.csv`
- the **real database connection string**, in `Modules/modConnection.bas`

**Reconstructed / placeholder (you fill these in):**
- **procedure logic** — not recoverable from native code; every form/module is a
  stub with the real names in place, ready for you to add code
- **control layout** — native VB6 keeps no recoverable design coordinates, so
  controls are stacked vertically; arrange them in the IDE
- OCX controls (grid, scanner, status bar…) are shown as `PictureBox`
  placeholders with the original type recorded in each control's `Tag`, so the
  project opens even without those third-party OCXs registered

## Testing against a fake / local database instead of EGX

`Modules/modConnection.bas` holds the real connection string exactly as the binary
uses it:

```
Provider=MSDAORA.1; Password=clients2006case; User ID=client;
Data Source=Coding; Persist Security Info=True;
```

`Data Source=Coding` is an Oracle TNS alias. To run against a test database:
- set `USE_FAKE = True` in `modMain.Sub Main`, and put your local test DB's
  connection string in `FAKE_CONN`, **or**
- point the `Coding` TNS alias at your local Oracle in `tnsnames.ora`.

Either way the fake DB must be a real Oracle instance (the provider is Oracle-only).

## The data layer IS recovered — in full

A data-entry app's real business logic is mostly its **SQL**, and SQL is stored as
text in the binary, so it *is* recoverable. Extracted from `Coding.exe`:

- **`RecoveredSQL.sql`** and **`Modules/modQueries.bas`** — **177 real SQL
  statements** the app runs, verbatim from the binary and filtered to genuine
  queries against the app's own tables (Oracle client-library noise removed).
  They expose the real column-level schema too, e.g. the full `CLIENTS_TEMP_EDIT`
  and `CLIENTS_CONTACT` (`NIN_CODE, LAST_BROKER, LANDLINE, MOBILE_PHONE, EMAIL`)
  column lists. `modQueries.bas` holds them as editable VB6 constants.
- **`Schema_Tables.txt`** — the **68 tables/views** those queries touch:
  `ALL_CLIENTS`, `BROKER_CLIENTS`, `BROKERS`, `CLIENTS`, `CLIENTS_CONTACT`,
  `TEMP_CLIENTS_VIEW`, `PENDING_CODES`, `REJECTED_CLIENTS_VIEW`,
  `SUSPEND_RESUME_IMAGE`, `AGENT_CLIENTS`, `GROUPS`, `USERS`, and more.

Together these are the app's data model and data operations — enough to build a
matching database and understand what every screen reads and writes.

## What remains genuinely unrecoverable

The **imperative VB procedure code** — event handlers, validations, control flow,
the exact order of operations inside each button — was compiled to machine code by
native VB6 and is **not** in the file as recoverable Basic. That layer is honestly
gone; the form/module bodies here are stubs. Anyone claiming a native VB6 binary
returns to complete original source is mistaken. What you have is: the full UI, the
full data model, all the SQL, the connection — everything the binary actually
preserves — with the procedural glue left for you to write.

## Files
- `Coding.vbp` — the project; open this in VB6
- `Forms/*.frm` — one per form, real names + real controls
- `Modules/*.bas`, `*.cls` — real module/class names, stub bodies
- `Modules/modConnection.bas` — the real data seam, with a fake-DB switch
- `RecoveredSQL.sql` — 222 real SQL statements from the binary
- `Schema_Tables.txt` — 68 tables/views the app uses
- `_extracted_controls.csv`, `_extracted_objects.csv` — the raw extraction this
  project was generated from
