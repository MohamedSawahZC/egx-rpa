VERSION 5.00
Begin VB.Form frmScreen38 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen38"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.PictureBox StatusBar1 
      Height          =   600
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
      Tag             =   "OCX:StatusBar"
   End
   Begin VB.Label lblTotal 
      Caption         =   "lblTotal"
      Height          =   255
      Left            =   240
      TabIndex        =   2
      Top             =   900
      Width           =   3000
   End
   Begin VB.Label Label3 
      Caption         =   "Label3"
      Height          =   255
      Left            =   240
      TabIndex        =   3
      Top             =   1215
      Width           =   3000
   End
   Begin VB.ComboBox cmbType 
      Height          =   315
      Left            =   240
      TabIndex        =   4
      Top             =   1530
      Width           =   3000
   End
   Begin VB.Label Label2 
      Caption         =   "Label2"
      Height          =   255
      Left            =   240
      TabIndex        =   5
      Top             =   1905
      Width           =   3000
   End
   Begin VB.CommandButton cmdRunExcel 
      Caption         =   "cmdRunExcel"
      Height          =   375
      Left            =   240
      TabIndex        =   6
      Top             =   2220
      Width           =   3000
   End
   Begin VB.CommandButton cmdUpdate 
      Caption         =   "cmdUpdate"
      Height          =   375
      Left            =   240
      TabIndex        =   7
      Top             =   2655
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen38"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen38  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
