VERSION 5.00
Begin VB.Form frmScreen60 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen60"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.ComboBox cmbTRDR 
      Height          =   315
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.TextBox txtTRDR 
      Height          =   285
      Left            =   240
      TabIndex        =   2
      Top             =   615
      Width           =   3000
   End
   Begin VB.Label lblRC 
      Caption         =   "lblRC"
      Height          =   255
      Left            =   240
      TabIndex        =   3
      Top             =   960
      Width           =   3000
   End
   Begin VB.Frame Frame1 
      Caption         =   "Frame1"
      Height          =   1500
      Left            =   240
      TabIndex        =   4
      Top             =   1275
      Width           =   3000
   End
   Begin VB.CommandButton cmdSearch 
      Caption         =   "cmdSearch"
      Height          =   375
      Left            =   240
      TabIndex        =   5
      Top             =   2835
      Width           =   3000
   End
   Begin VB.PictureBox tlbDocs 
      Height          =   600
      Left            =   240
      TabIndex        =   6
      Top             =   3270
      Width           =   3000
      Tag             =   "OCX:Toolbar"
   End
   Begin VB.Label lblCount 
      Caption         =   "lblCount"
      Height          =   255
      Left            =   240
      TabIndex        =   7
      Top             =   3930
      Width           =   3000
   End
   Begin VB.Label lblReject 
      Caption         =   "lblReject"
      Height          =   255
      Left            =   240
      TabIndex        =   8
      Top             =   4245
      Width           =   3000
   End
   Begin VB.Label lblImgCount 
      Caption         =   "lblImgCount"
      Height          =   255
      Left            =   240
      TabIndex        =   9
      Top             =   4560
      Width           =   3000
   End
   Begin VB.Label lblImgCountAgent 
      Caption         =   "lblImgCountAgent"
      Height          =   255
      Left            =   240
      TabIndex        =   10
      Top             =   4875
      Width           =   3000
   End
   Begin VB.TextBox txtName 
      Height          =   285
      Left            =   240
      TabIndex        =   11
      Top             =   5190
      Width           =   3000
   End
   Begin VB.FileListBox File1 
      Height          =   600
      Left            =   240
      TabIndex        =   12
      Top             =   5535
      Width           =   3000
   End
   Begin VB.Frame Frame4 
      Caption         =   "Frame4"
      Height          =   1500
      Left            =   240
      TabIndex        =   13
      Top             =   6195
      Width           =   3000
   End
   Begin VB.Frame Frame3 
      Caption         =   "Frame3"
      Height          =   1500
      Left            =   240
      TabIndex        =   14
      Top             =   7755
      Width           =   3000
   End
   Begin VB.Frame Frame5 
      Caption         =   "Frame5"
      Height          =   1500
      Left            =   240
      TabIndex        =   15
      Top             =   9315
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen60"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen60  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
