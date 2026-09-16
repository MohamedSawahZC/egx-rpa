VERSION 5.00
Begin VB.Form frmScreen59 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen59"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.CheckBox chkClose 
      Caption         =   "chkClose"
      Height          =   255
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.OptionButton optImage 
      Caption         =   "optImage"
      Height          =   255
      Left            =   240
      TabIndex        =   2
      Top             =   555
      Width           =   3000
   End
   Begin VB.OptionButton optPDF 
      Caption         =   "optPDF"
      Height          =   255
      Left            =   240
      TabIndex        =   3
      Top             =   870
      Width           =   3000
   End
   Begin VB.TextBox txtMsg 
      Height          =   285
      Left            =   240
      TabIndex        =   4
      Top             =   1185
      Width           =   3000
   End
   Begin VB.Label Label2 
      Caption         =   "Label2"
      Height          =   255
      Left            =   240
      TabIndex        =   5
      Top             =   1530
      Width           =   3000
   End
   Begin VB.Image Image1 
      Height          =   600
      Left            =   240
      TabIndex        =   6
      Top             =   1845
      Width           =   3000
   End
   Begin VB.Image Image2 
      Height          =   600
      Left            =   240
      TabIndex        =   7
      Top             =   2505
      Width           =   3000
   End
   Begin VB.Frame Frame1 
      Caption         =   "Frame1"
      Height          =   1500
      Left            =   240
      TabIndex        =   8
      Top             =   3165
      Width           =   3000
   End
   Begin VB.OptionButton optExcel 
      Caption         =   "optExcel"
      Height          =   255
      Left            =   240
      TabIndex        =   9
      Top             =   4725
      Width           =   3000
   End
   Begin VB.CommandButton cmdSave 
      Caption         =   "cmdSave"
      Height          =   375
      Left            =   240
      TabIndex        =   10
      Top             =   5040
      Width           =   3000
   End
   Begin VB.CommandButton cmdBack 
      Caption         =   "cmdBack"
      Height          =   375
      Left            =   240
      TabIndex        =   11
      Top             =   5475
      Width           =   3000
   End
   Begin VB.Label Label4 
      Caption         =   "Label4"
      Height          =   255
      Left            =   240
      TabIndex        =   12
      Top             =   5910
      Width           =   3000
   End
   Begin VB.FileListBox File1 
      Height          =   600
      Left            =   240
      TabIndex        =   13
      Top             =   6225
      Width           =   3000
   End
   Begin VB.OptionButton optAll 
      Caption         =   "optAll"
      Height          =   255
      Left            =   240
      TabIndex        =   14
      Top             =   6885
      Width           =   3000
   End
   Begin VB.ListBox lstSelected 
      Height          =   600
      Left            =   240
      TabIndex        =   15
      Top             =   7200
      Width           =   3000
   End
   Begin VB.Label Label1 
      Caption         =   "Label1"
      Height          =   255
      Left            =   240
      TabIndex        =   16
      Top             =   7860
      Width           =   3000
   End
   Begin VB.OptionButton optOne 
      Caption         =   "optOne"
      Height          =   255
      Left            =   240
      TabIndex        =   17
      Top             =   8175
      Width           =   3000
   End
   Begin VB.Frame fraMessageImage 
      Caption         =   "fraMessageImage"
      Height          =   1500
      Left            =   240
      TabIndex        =   18
      Top             =   8490
      Width           =   3000
   End
   Begin VB.ListBox lstCompanies 
      Height          =   600
      Left            =   240
      TabIndex        =   19
      Top             =   10050
      Width           =   3000
   End
   Begin VB.Frame Frame3 
      Caption         =   "Frame3"
      Height          =   1500
      Left            =   240
      TabIndex        =   20
      Top             =   10710
      Width           =   3000
   End
   Begin VB.PictureBox Scan1 
      Height          =   600
      Left            =   240
      TabIndex        =   21
      Top             =   12270
      Width           =   3000
      Tag             =   "OCX:ScanControl"
   End
End
Attribute VB_Name = "frmScreen59"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen59  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
