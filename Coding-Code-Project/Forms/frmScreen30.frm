VERSION 5.00
Begin VB.Form frmScreen30 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen30"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.Label Label1 
      Caption         =   "Label1"
      Height          =   255
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.Label lblTotal 
      Caption         =   "lblTotal"
      Height          =   255
      Left            =   240
      TabIndex        =   2
      Top             =   555
      Width           =   3000
   End
   Begin VB.ComboBox cmbDays 
      Height          =   315
      Left            =   240
      TabIndex        =   3
      Top             =   870
      Width           =   3000
   End
   Begin VB.TextBox txtMin 
      Height          =   285
      Left            =   240
      TabIndex        =   4
      Top             =   1245
      Width           =   3000
   End
   Begin VB.Label Label2 
      Caption         =   "Label2"
      Height          =   255
      Left            =   240
      TabIndex        =   5
      Top             =   1590
      Width           =   3000
   End
   Begin VB.CommandButton cmdUpdate 
      Caption         =   "cmdUpdate"
      Height          =   375
      Left            =   240
      TabIndex        =   6
      Top             =   1905
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen30"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen30  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
