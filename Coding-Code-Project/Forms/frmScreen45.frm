VERSION 5.00
Begin VB.Form frmScreen45 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen45"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.CheckBox chkALT 
      Caption         =   "chkALT"
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
   Begin VB.PictureBox FG1 
      Height          =   600
      Left            =   240
      TabIndex        =   3
      Top             =   1215
      Width           =   3000
      Tag             =   "OCX:FlexGrid"
   End
   Begin VB.ComboBox cmbTRDR 
      Height          =   315
      Left            =   240
      TabIndex        =   4
      Top             =   1875
      Width           =   3000
   End
   Begin VB.Image imgTest 
      Height          =   600
      Left            =   240
      TabIndex        =   5
      Top             =   2250
      Width           =   3000
   End
   Begin VB.Label lblCount 
      Caption         =   "lblCount"
      Height          =   255
      Left            =   240
      TabIndex        =   6
      Top             =   2910
      Width           =   3000
   End
   Begin VB.FileListBox File1 
      Height          =   600
      Left            =   240
      TabIndex        =   7
      Top             =   3225
      Width           =   3000
   End
   Begin VB.CommandButton cmdSearch 
      Caption         =   "cmdSearch"
      Height          =   375
      Left            =   240
      TabIndex        =   8
      Top             =   3885
      Width           =   3000
   End
   Begin VB.TextBox txtName 
      Height          =   285
      Left            =   240
      TabIndex        =   9
      Top             =   4320
      Width           =   3000
   End
   Begin VB.Label lbl 
      Caption         =   "lbl"
      Height          =   255
      Left            =   240
      TabIndex        =   10
      Top             =   4665
      Width           =   3000
   End
   Begin VB.Label lblDate 
      Caption         =   "lblDate"
      Height          =   255
      Left            =   240
      TabIndex        =   11
      Top             =   4980
      Width           =   3000
   End
   Begin VB.Frame Frame2 
      Caption         =   "Frame2"
      Height          =   1500
      Left            =   240
      TabIndex        =   12
      Top             =   5295
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen45"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen45  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
