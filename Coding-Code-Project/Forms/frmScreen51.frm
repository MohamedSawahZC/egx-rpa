VERSION 5.00
Begin VB.Form frmScreen51 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen51"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.CommandButton cmdPrint 
      Caption         =   "cmdPrint"
      Height          =   375
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.Label lblHeader 
      Caption         =   "lblHeader"
      Height          =   255
      Left            =   240
      TabIndex        =   2
      Top             =   675
      Width           =   3000
   End
   Begin VB.CheckBox chkLandScape 
      Caption         =   "chkLandScape"
      Height          =   255
      Left            =   240
      TabIndex        =   3
      Top             =   990
      Width           =   3000
   End
   Begin VB.Label lblStoped 
      Caption         =   "lblStoped"
      Height          =   255
      Left            =   240
      TabIndex        =   4
      Top             =   1305
      Width           =   3000
   End
   Begin VB.Label lblTotal 
      Caption         =   "lblTotal"
      Height          =   255
      Left            =   240
      TabIndex        =   5
      Top             =   1620
      Width           =   3000
   End
   Begin VB.Label lblRemarks 
      Caption         =   "lblRemarks"
      Height          =   255
      Left            =   240
      TabIndex        =   6
      Top             =   1935
      Width           =   3000
   End
   Begin VB.Label Label4 
      Caption         =   "Label4"
      Height          =   255
      Left            =   240
      TabIndex        =   7
      Top             =   2250
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen51"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen51  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
