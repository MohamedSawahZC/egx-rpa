Attribute VB_Name = "modConnection"
' ============================================================================
'  modConnection - the database seam recovered from Coding.exe
'
'  The REAL connection string, read verbatim from the binary:
'     Provider=MSDAORA.1;Password=clients2006case;User ID=client;
'     Data Source=Coding;Persist Security Info=True;
'
'  'Data Source=Coding' is a TNS alias. To test against a FAKE / LOCAL
'  database instead of EGX, either point the 'Coding' alias at your local
'  Oracle in tnsnames.ora, or change USE_FAKE = True below and set the
'  fake connection string to your own test database.
' ============================================================================
Option Explicit

Public Const REAL_CONN As String = "Provider=MSDAORA.1;Password=clients2006case;User ID=client;Data Source=Coding;Persist Security Info=True;"

'  Point this at whatever test DB you stand up locally (Oracle XE, or via ODBC).
Public Const FAKE_CONN As String = "Provider=MSDAORA.1;Password=test;User ID=test;Data Source=LOCALTEST;Persist Security Info=True;"

Public USE_FAKE As Boolean   '  set True to test against the fake/local DB
Public gConn As Object       '  ADODB.Connection

Public Function OpenConnection() As Boolean
    On Error GoTo Failed
    Set gConn = CreateObject("ADODB.Connection")
    If USE_FAKE Then
        gConn.Open FAKE_CONN
    Else
        gConn.Open REAL_CONN
    End If
    OpenConnection = (gConn.State = 1)   ' adStateOpen
    Exit Function
Failed:
    MsgBox "Connection Error: " & Err.Description, vbExclamation
    OpenConnection = False
End Function
