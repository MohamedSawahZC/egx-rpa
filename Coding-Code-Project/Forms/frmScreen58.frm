VERSION 5.00
Begin VB.Form frmScreen58 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen58"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.Label lblRC 
      Caption         =   "lblRC"
      Height          =   255
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.FileListBox File1 
      Height          =   600
      Left            =   240
      TabIndex        =   2
      Top             =   555
      Width           =   3000
   End
   Begin VB.PictureBox tlbDocs 
      Height          =   600
      Left            =   240
      TabIndex        =   3
      Top             =   1215
      Width           =   3000
      Tag             =   "OCX:Toolbar"
   End
   Begin VB.PictureBox ProgressBar1 
      Height          =   600
      Left            =   240
      TabIndex        =   4
      Top             =   1875
      Width           =   3000
      Tag             =   "OCX:ProgressBar"
   End
   Begin VB.TextBox txtName 
      Height          =   285
      Left            =   240
      TabIndex        =   5
      Top             =   2535
      Width           =   3000
   End
   Begin VB.Frame Frame1 
      Caption         =   "Frame1"
      Height          =   1500
      Left            =   240
      TabIndex        =   6
      Top             =   2880
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen58"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen58  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
