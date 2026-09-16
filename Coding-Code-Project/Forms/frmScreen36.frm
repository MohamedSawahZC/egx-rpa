VERSION 5.00
Begin VB.Form frmScreen36 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen36"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.Frame Frame2 
      Caption         =   "Frame2"
      Height          =   1500
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.PictureBox fg 
      Height          =   600
      Left            =   240
      TabIndex        =   2
      Top             =   1800
      Width           =   3000
      Tag             =   "OCX:FlexGrid"
   End
   Begin VB.CommandButton cmdSave 
      Caption         =   "cmdSave"
      Height          =   375
      Left            =   240
      TabIndex        =   3
      Top             =   2460
      Width           =   3000
   End
   Begin VB.TextBox txtPass 
      Height          =   285
      Left            =   240
      TabIndex        =   4
      Top             =   2895
      Width           =   3000
   End
   Begin VB.TextBox txtIP 
      Height          =   285
      Left            =   240
      TabIndex        =   5
      Top             =   3240
      Width           =   3000
   End
   Begin VB.TextBox txtUser 
      Height          =   285
      Left            =   240
      TabIndex        =   6
      Top             =   3585
      Width           =   3000
   End
   Begin VB.Frame Frame1 
      Caption         =   "Frame1"
      Height          =   1500
      Left            =   240
      TabIndex        =   7
      Top             =   3930
      Width           =   3000
   End
   Begin VB.TextBox txtBroker 
      Height          =   285
      Left            =   240
      TabIndex        =   8
      Top             =   5490
      Width           =   3000
   End
   Begin VB.CheckBox chkSuspend 
      Caption         =   "chkSuspend"
      Height          =   255
      Left            =   240
      TabIndex        =   9
      Top             =   5835
      Width           =   3000
   End
   Begin VB.Label lbl 
      Caption         =   "lbl"
      Height          =   255
      Left            =   240
      TabIndex        =   10
      Top             =   6150
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen36"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen36  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
