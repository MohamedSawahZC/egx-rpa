VERSION 5.00
Begin VB.Form frmScreen20 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen20"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.PictureBox fg 
      Height          =   600
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
      Tag             =   "OCX:FlexGrid"
   End
   Begin VB.ComboBox cmbType 
      Height          =   315
      Left            =   240
      TabIndex        =   2
      Top             =   900
      Width           =   3000
   End
   Begin VB.CommandButton cmdSave 
      Caption         =   "cmdSave"
      Height          =   375
      Left            =   240
      TabIndex        =   3
      Top             =   1275
      Width           =   3000
   End
   Begin VB.Frame Frame2 
      Caption         =   "Frame2"
      Height          =   1500
      Left            =   240
      TabIndex        =   4
      Top             =   1710
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen20"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen20  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
