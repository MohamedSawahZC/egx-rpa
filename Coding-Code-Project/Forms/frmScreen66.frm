VERSION 5.00
Begin VB.Form frmScreen66 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen66"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.Label Label2 
      Caption         =   "Label2"
      Height          =   255
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.PictureBox StatusBar1 
      Height          =   600
      Left            =   240
      TabIndex        =   2
      Top             =   555
      Width           =   3000
      Tag             =   "OCX:StatusBar"
   End
   Begin VB.Label lbl 
      Caption         =   "lbl"
      Height          =   255
      Left            =   240
      TabIndex        =   3
      Top             =   1215
      Width           =   3000
   End
   Begin VB.Frame fraAgent 
      Caption         =   "fraAgent"
      Height          =   1500
      Left            =   240
      TabIndex        =   4
      Top             =   1530
      Width           =   3000
   End
   Begin VB.CheckBox chkAgent 
      Caption         =   "chkAgent"
      Height          =   255
      Left            =   240
      TabIndex        =   5
      Top             =   3090
      Width           =   3000
   End
   Begin VB.Label lblExpireDate 
      Caption         =   "lblExpireDate"
      Height          =   255
      Left            =   240
      TabIndex        =   6
      Top             =   3405
      Width           =   3000
   End
   Begin VB.Label lblIssueDate 
      Caption         =   "lblIssueDate"
      Height          =   255
      Left            =   240
      TabIndex        =   7
      Top             =   3720
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen66"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen66  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
