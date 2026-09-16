Attribute VB_Name = "modMain"
Option Explicit

' ============================================================================
'  modMain - reconstructed entry point for the Coding project.
'
'  BYPASS_LOGIN below is the switch you asked for. When True, Sub Main does NOT
'  connect to the EGX database and does NOT show a login prompt - it opens the
'  UI directly. This is how you run the reversed app offline with login skipped.
'
'  The real Coding.exe cannot do this (it connects to EGX's Oracle in startup
'  before drawing anything). This reconstructed project can, because here WE own
'  the startup code - so the bypass lives in real, editable source, not a hack.
' ============================================================================

Public Const BYPASS_LOGIN As Boolean = True    ' <-- run offline, no DB, no login

Sub Main()
    If BYPASS_LOGIN Then
        ' --- offline bypass: straight to the UI, no database, no login ---
        On Error Resume Next
        frmMain.Show                 ' the MDI frame / main screen
        Exit Sub
    End If

    ' --- normal path (needs the EGX database, or a local one via modConnection) ---
    modConnection.USE_FAKE = True     ' True = local/fake DB; False = real EGX
    If Not modConnection.OpenConnection() Then
        MsgBox "Connection Error", vbExclamation
        End
    End If
    frmLogin.Show                     ' the real app authenticates here
End Sub
