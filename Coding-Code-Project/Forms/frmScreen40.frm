VERSION 5.00
Begin VB.Form frmScreen40 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen40"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.Label lblDir 
      Caption         =   "lblDir"
      Height          =   255
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.TextBox txtExcelDirectory 
      Height          =   285
      Left            =   240
      TabIndex        =   2
      Top             =   555
      Width           =   3000
   End
   Begin VB.CommandButton cmdRunReport 
      Caption         =   "cmdRunReport"
      Height          =   375
      Left            =   240
      TabIndex        =   3
      Top             =   900
      Width           =   3000
   End
   Begin VB.Frame Frame1 
      Caption         =   "Frame1"
      Height          =   1500
      Left            =   240
      TabIndex        =   4
      Top             =   1335
      Width           =   3000
   End
   Begin VB.TextBox txt 
      Height          =   285
      Left            =   240
      TabIndex        =   5
      Top             =   2895
      Width           =   3000
   End
   Begin VB.CheckBox chkReactivate 
      Caption         =   "chkReactivate"
      Height          =   255
      Left            =   240
      TabIndex        =   6
      Top             =   3240
      Width           =   3000
   End
   Begin VB.CheckBox chkInsert 
      Caption         =   "chkInsert"
      Height          =   255
      Left            =   240
      TabIndex        =   7
      Top             =   3555
      Width           =   3000
   End
   Begin VB.Label lbl 
      Caption         =   "lbl"
      Height          =   255
      Left            =   240
      TabIndex        =   8
      Top             =   3870
      Width           =   3000
   End
   Begin VB.CheckBox chkGet 
      Caption         =   "chkGet"
      Height          =   255
      Left            =   240
      TabIndex        =   9
      Top             =   4185
      Width           =   3000
   End
   Begin VB.Label lblDate 
      Caption         =   "lblDate"
      Height          =   255
      Left            =   240
      TabIndex        =   10
      Top             =   4500
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen40"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen40  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
