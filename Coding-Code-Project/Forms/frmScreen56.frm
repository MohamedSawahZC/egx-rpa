VERSION 5.00
Begin VB.Form frmScreen56 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen56"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.CheckBox chkFiles 
      Caption         =   "chkFiles"
      Height          =   255
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.PictureBox Scan1 
      Height          =   600
      Left            =   240
      TabIndex        =   2
      Top             =   555
      Width           =   3000
      Tag             =   "OCX:ScanControl"
   End
   Begin VB.Label Label4 
      Caption         =   "Label4"
      Height          =   255
      Left            =   240
      TabIndex        =   3
      Top             =   1215
      Width           =   3000
   End
   Begin VB.TextBox txtNIN 
      Height          =   285
      Left            =   240
      TabIndex        =   4
      Top             =   1530
      Width           =   3000
   End
   Begin VB.Label Label5 
      Caption         =   "Label5"
      Height          =   255
      Left            =   240
      TabIndex        =   5
      Top             =   1875
      Width           =   3000
   End
   Begin VB.CommandButton cmdSearch 
      Caption         =   "cmdSearch"
      Height          =   375
      Left            =   240
      TabIndex        =   6
      Top             =   2190
      Width           =   3000
   End
   Begin VB.Frame Frame1 
      Caption         =   "Frame1"
      Height          =   1500
      Left            =   240
      TabIndex        =   7
      Top             =   2625
      Width           =   3000
   End
   Begin VB.CommandButton cmdSave 
      Caption         =   "cmdSave"
      Height          =   375
      Left            =   240
      TabIndex        =   8
      Top             =   4185
      Width           =   3000
   End
   Begin VB.CommandButton cmdBack 
      Caption         =   "cmdBack"
      Height          =   375
      Left            =   240
      TabIndex        =   9
      Top             =   4620
      Width           =   3000
   End
   Begin VB.Label lblName 
      Caption         =   "lblName"
      Height          =   255
      Left            =   240
      TabIndex        =   10
      Top             =   5055
      Width           =   3000
   End
   Begin VB.Label lblImgCount 
      Caption         =   "lblImgCount"
      Height          =   255
      Left            =   240
      TabIndex        =   11
      Top             =   5370
      Width           =   3000
   End
   Begin VB.CommandButton cmdAdd 
      Caption         =   "cmdAdd"
      Height          =   375
      Left            =   240
      TabIndex        =   12
      Top             =   5685
      Width           =   3000
   End
   Begin VB.CommandButton cmdPrv 
      Caption         =   "cmdPrv"
      Height          =   375
      Left            =   240
      TabIndex        =   13
      Top             =   6120
      Width           =   3000
   End
   Begin VB.CommandButton cmdDel 
      Caption         =   "cmdDel"
      Height          =   375
      Left            =   240
      TabIndex        =   14
      Top             =   6555
      Width           =   3000
   End
   Begin VB.Image img 
      Height          =   600
      Left            =   240
      TabIndex        =   15
      Top             =   6990
      Width           =   3000
   End
   Begin VB.TextBox txtReason 
      Height          =   285
      Left            =   240
      TabIndex        =   16
      Top             =   7650
      Width           =   3000
   End
   Begin VB.Label Label3 
      Caption         =   "Label3"
      Height          =   255
      Left            =   240
      TabIndex        =   17
      Top             =   7995
      Width           =   3000
   End
   Begin VB.Label Label2 
      Caption         =   "Label2"
      Height          =   255
      Left            =   240
      TabIndex        =   18
      Top             =   8310
      Width           =   3000
   End
   Begin VB.ComboBox cmbType 
      Height          =   315
      Left            =   240
      TabIndex        =   19
      Top             =   8625
      Width           =   3000
   End
   Begin VB.Label lblReason 
      Caption         =   "lblReason"
      Height          =   255
      Left            =   240
      TabIndex        =   20
      Top             =   9000
      Width           =   3000
   End
   Begin VB.FileListBox File1 
      Height          =   600
      Left            =   240
      TabIndex        =   21
      Top             =   9315
      Width           =   3000
   End
   Begin VB.Image imgTest 
      Height          =   600
      Left            =   240
      TabIndex        =   22
      Top             =   9975
      Width           =   3000
   End
   Begin VB.ComboBox cmbReason 
      Height          =   315
      Left            =   240
      TabIndex        =   23
      Top             =   10635
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen56"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen56  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
