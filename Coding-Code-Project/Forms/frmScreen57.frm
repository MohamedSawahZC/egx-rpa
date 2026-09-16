VERSION 5.00
Begin VB.Form frmScreen57 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen57"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.CommandButton CancelButton 
      Caption         =   "CancelButton"
      Height          =   375
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.Image img 
      Height          =   600
      Left            =   240
      TabIndex        =   2
      Top             =   675
      Width           =   3000
   End
   Begin VB.Image imgTest 
      Height          =   600
      Left            =   240
      TabIndex        =   3
      Top             =   1335
      Width           =   3000
   End
   Begin VB.CommandButton OKButton 
      Caption         =   "OKButton"
      Height          =   375
      Left            =   240
      TabIndex        =   4
      Top             =   1995
      Width           =   3000
   End
   Begin VB.CommandButton cmdDel 
      Caption         =   "cmdDel"
      Height          =   375
      Left            =   240
      TabIndex        =   5
      Top             =   2430
      Width           =   3000
   End
   Begin VB.Label Label2 
      Caption         =   "Label2"
      Height          =   255
      Left            =   240
      TabIndex        =   6
      Top             =   2865
      Width           =   3000
   End
   Begin VB.Label Label3 
      Caption         =   "Label3"
      Height          =   255
      Left            =   240
      TabIndex        =   7
      Top             =   3180
      Width           =   3000
   End
   Begin VB.Label Label1 
      Caption         =   "Label1"
      Height          =   255
      Left            =   240
      TabIndex        =   8
      Top             =   3495
      Width           =   3000
   End
   Begin VB.CheckBox chkTemp 
      Caption         =   "chkTemp"
      Height          =   255
      Left            =   240
      TabIndex        =   9
      Top             =   3810
      Width           =   3000
   End
   Begin VB.CheckBox chkSuspend 
      Caption         =   "chkSuspend"
      Height          =   255
      Left            =   240
      TabIndex        =   10
      Top             =   4125
      Width           =   3000
   End
   Begin VB.Label Label6 
      Caption         =   "Label6"
      Height          =   255
      Left            =   240
      TabIndex        =   11
      Top             =   4440
      Width           =   3000
   End
   Begin VB.Label Label5 
      Caption         =   "Label5"
      Height          =   255
      Left            =   240
      TabIndex        =   12
      Top             =   4755
      Width           =   3000
   End
   Begin VB.ComboBox cmbGroup 
      Height          =   315
      Left            =   240
      TabIndex        =   13
      Top             =   5070
      Width           =   3000
   End
   Begin VB.TextBox txtComment 
      Height          =   285
      Left            =   240
      TabIndex        =   14
      Top             =   5445
      Width           =   3000
   End
   Begin VB.FileListBox File1 
      Height          =   600
      Left            =   240
      TabIndex        =   15
      Top             =   5790
      Width           =   3000
   End
   Begin VB.CheckBox chkFiles 
      Caption         =   "chkFiles"
      Height          =   255
      Left            =   240
      TabIndex        =   16
      Top             =   6450
      Width           =   3000
   End
   Begin VB.PictureBox Scan1 
      Height          =   600
      Left            =   240
      TabIndex        =   17
      Top             =   6765
      Width           =   3000
      Tag             =   "OCX:ScanControl"
   End
   Begin VB.PictureBox grdMain 
      Height          =   600
      Left            =   240
      TabIndex        =   18
      Top             =   7425
      Width           =   3000
      Tag             =   "OCX:FlexGrid"
   End
   Begin VB.Label Label4 
      Caption         =   "Label4"
      Height          =   255
      Left            =   240
      TabIndex        =   19
      Top             =   8085
      Width           =   3000
   End
   Begin VB.Label lblDate 
      Caption         =   "lblDate"
      Height          =   255
      Left            =   240
      TabIndex        =   20
      Top             =   8400
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen57"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen57  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
