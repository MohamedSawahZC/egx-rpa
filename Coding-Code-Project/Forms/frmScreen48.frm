VERSION 5.00
Begin VB.Form frmScreen48 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen48"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.Label lblDev 
      Caption         =   "lblDev"
      Height          =   255
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.Label lblTitle 
      Caption         =   "lblTitle"
      Height          =   255
      Left            =   240
      TabIndex        =   2
      Top             =   555
      Width           =   3000
   End
   Begin VB.Label lblVersion 
      Caption         =   "lblVersion"
      Height          =   255
      Left            =   240
      TabIndex        =   3
      Top             =   870
      Width           =   3000
   End
   Begin VB.CommandButton cmdOK 
      Caption         =   "cmdOK"
      Height          =   375
      Left            =   240
      TabIndex        =   4
      Top             =   1185
      Width           =   3000
   End
   Begin VB.TextBox txtMobile 
      Height          =   285
      Left            =   240
      TabIndex        =   5
      Top             =   1620
      Width           =   3000
   End
   Begin VB.Label Label5 
      Caption         =   "Label5"
      Height          =   255
      Left            =   240
      TabIndex        =   6
      Top             =   1965
      Width           =   3000
   End
   Begin VB.Label Label2 
      Caption         =   "Label2"
      Height          =   255
      Left            =   240
      TabIndex        =   7
      Top             =   2280
      Width           =   3000
   End
   Begin VB.Label Label4 
      Caption         =   "Label4"
      Height          =   255
      Left            =   240
      TabIndex        =   8
      Top             =   2595
      Width           =   3000
   End
   Begin VB.Label Label1 
      Caption         =   "Label1"
      Height          =   255
      Left            =   240
      TabIndex        =   9
      Top             =   2910
      Width           =   3000
   End
   Begin VB.Image Image1 
      Height          =   600
      Left            =   240
      TabIndex        =   10
      Top             =   3225
      Width           =   3000
   End
   Begin VB.TextBox txtJob 
      Height          =   285
      Left            =   240
      TabIndex        =   11
      Top             =   3885
      Width           =   3000
   End
   Begin VB.Image Image2 
      Height          =   600
      Left            =   240
      TabIndex        =   12
      Top             =   4230
      Width           =   3000
   End
   Begin VB.Timer Timer1 
      Height          =   600
      Left            =   240
      TabIndex        =   13
      Top             =   4890
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen48"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen48  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
