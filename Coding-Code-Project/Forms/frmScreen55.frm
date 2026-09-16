VERSION 5.00
Begin VB.Form frmScreen55 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen55"
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
   Begin VB.Frame Frame2 
      Caption         =   "Frame2"
      Height          =   1500
      Left            =   240
      TabIndex        =   2
      Top             =   900
      Width           =   3000
   End
   Begin VB.PictureBox ProgressBar1 
      Height          =   600
      Left            =   240
      TabIndex        =   3
      Top             =   2460
      Width           =   3000
      Tag             =   "OCX:ProgressBar"
   End
   Begin VB.Image ImgView2 
      Height          =   600
      Left            =   240
      TabIndex        =   4
      Top             =   3120
      Width           =   3000
   End
   Begin VB.Label lblCurrImg 
      Caption         =   "lblCurrImg"
      Height          =   255
      Left            =   240
      TabIndex        =   5
      Top             =   3780
      Width           =   3000
   End
   Begin VB.PictureBox FG1 
      Height          =   600
      Left            =   240
      TabIndex        =   6
      Top             =   4095
      Width           =   3000
      Tag             =   "OCX:FlexGrid"
   End
   Begin VB.TextBox txtTRDR 
      Height          =   285
      Left            =   240
      TabIndex        =   7
      Top             =   4755
      Width           =   3000
   End
   Begin VB.Label lblRC 
      Caption         =   "lblRC"
      Height          =   255
      Left            =   240
      TabIndex        =   8
      Top             =   5100
      Width           =   3000
   End
   Begin VB.PictureBox tlbDocs 
      Height          =   600
      Left            =   240
      TabIndex        =   9
      Top             =   5415
      Width           =   3000
      Tag             =   "OCX:Toolbar"
   End
   Begin VB.Image ImgView 
      Height          =   600
      Left            =   240
      TabIndex        =   10
      Top             =   6075
      Width           =   3000
   End
   Begin VB.Label lblReject 
      Caption         =   "lblReject"
      Height          =   255
      Left            =   240
      TabIndex        =   11
      Top             =   6735
      Width           =   3000
   End
   Begin VB.FileListBox File1 
      Height          =   600
      Left            =   240
      TabIndex        =   12
      Top             =   7050
      Width           =   3000
   End
   Begin VB.Frame Frame5 
      Caption         =   "Frame5"
      Height          =   1500
      Left            =   240
      TabIndex        =   13
      Top             =   7710
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen55"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen55  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
