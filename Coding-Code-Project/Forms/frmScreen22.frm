VERSION 5.00
Begin VB.Form frmScreen22 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen22"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.TextBox txt 
      Height          =   285
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.Frame Frame2 
      Caption         =   "Frame2"
      Height          =   1500
      Left            =   240
      TabIndex        =   2
      Top             =   585
      Width           =   3000
   End
   Begin VB.CommandButton cmdSave 
      Caption         =   "cmdSave"
      Height          =   375
      Left            =   240
      TabIndex        =   3
      Top             =   2145
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen22"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen22  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
