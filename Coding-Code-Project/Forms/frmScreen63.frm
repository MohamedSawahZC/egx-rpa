VERSION 5.00
Begin VB.Form frmScreen63 
   ClientHeight    =   6000
   ClientLeft      =   120
   ClientTop       =   450
   ClientWidth     =   9000
   LinkTopic       =   "Form1"
   ScaleHeight     =   6000
   ScaleWidth      =   9000
   StartUpPosition =   3  'Windows Default
   Caption         =   "frmScreen63"
   BeginProperty Font
      Name            =   "Tahoma"
      Size            =   9
   EndProperty
   RightToLeft     =   -1  'True
   Begin VB.Label Label7 
      Caption         =   "Label7"
      Height          =   255
      Left            =   240
      TabIndex        =   1
      Top             =   240
      Width           =   3000
   End
   Begin VB.Label Label5 
      Caption         =   "Label5"
      Height          =   255
      Left            =   240
      TabIndex        =   2
      Top             =   555
      Width           =   3000
   End
   Begin VB.Label Label6 
      Caption         =   "Label6"
      Height          =   255
      Left            =   240
      TabIndex        =   3
      Top             =   870
      Width           =   3000
   End
   Begin VB.Label Label2 
      Caption         =   "Label2"
      Height          =   255
      Left            =   240
      TabIndex        =   4
      Top             =   1185
      Width           =   3000
   End
   Begin VB.TextBox txt 
      Height          =   285
      Left            =   240
      TabIndex        =   5
      Top             =   1500
      Width           =   3000
   End
   Begin VB.Label Label4 
      Caption         =   "Label4"
      Height          =   255
      Left            =   240
      TabIndex        =   6
      Top             =   1845
      Width           =   3000
   End
   Begin VB.PictureBox picContainer 
      Height          =   600
      Left            =   240
      TabIndex        =   7
      Top             =   2160
      Width           =   3000
   End
   Begin VB.CheckBox chkAgree 
      Caption         =   "chkAgree"
      Height          =   255
      Left            =   240
      TabIndex        =   8
      Top             =   2820
      Width           =   3000
   End
   Begin VB.ComboBox cmb 
      Height          =   315
      Left            =   240
      TabIndex        =   9
      Top             =   3135
      Width           =   3000
   End
   Begin VB.Label Label8 
      Caption         =   "Label8"
      Height          =   255
      Left            =   240
      TabIndex        =   10
      Top             =   3510
      Width           =   3000
   End
   Begin VB.Label Label9 
      Caption         =   "Label9"
      Height          =   255
      Left            =   240
      TabIndex        =   11
      Top             =   3825
      Width           =   3000
   End
   Begin VB.TextBox txtTel1 
      Height          =   285
      Left            =   240
      TabIndex        =   12
      Top             =   4140
      Width           =   3000
   End
   Begin VB.Label Label12 
      Caption         =   "Label12"
      Height          =   255
      Left            =   240
      TabIndex        =   13
      Top             =   4485
      Width           =   3000
   End
   Begin VB.TextBox txtTel2 
      Height          =   285
      Left            =   240
      TabIndex        =   14
      Top             =   4800
      Width           =   3000
   End
   Begin VB.TextBox txtAgreement 
      Height          =   285
      Left            =   240
      TabIndex        =   15
      Top             =   5145
      Width           =   3000
   End
   Begin VB.TextBox txtFax 
      Height          =   285
      Left            =   240
      TabIndex        =   16
      Top             =   5490
      Width           =   3000
   End
   Begin VB.TextBox txtContName 
      Height          =   285
      Left            =   240
      TabIndex        =   17
      Top             =   5835
      Width           =   3000
   End
   Begin VB.TextBox txtContTel 
      Height          =   285
      Left            =   240
      TabIndex        =   18
      Top             =   6180
      Width           =   3000
   End
   Begin VB.PictureBox picHide 
      Height          =   600
      Left            =   240
      TabIndex        =   19
      Top             =   6525
      Width           =   3000
   End
   Begin VB.TextBox txtContMob 
      Height          =   285
      Left            =   240
      TabIndex        =   20
      Top             =   7185
      Width           =   3000
   End
   Begin VB.TextBox txtEmail 
      Height          =   285
      Left            =   240
      TabIndex        =   21
      Top             =   7530
      Width           =   3000
   End
   Begin VB.Label lblHint 
      Caption         =   "lblHint"
      Height          =   255
      Left            =   240
      TabIndex        =   22
      Top             =   7875
      Width           =   3000
   End
   Begin VB.Label Label11 
      Caption         =   "Label11"
      Height          =   255
      Left            =   240
      TabIndex        =   23
      Top             =   8190
      Width           =   3000
   End
   Begin VB.Image Image2 
      Height          =   600
      Left            =   240
      TabIndex        =   24
      Top             =   8505
      Width           =   3000
   End
   Begin VB.Frame fraBroker 
      Caption         =   "fraBroker"
      Height          =   1500
      Left            =   240
      TabIndex        =   25
      Top             =   9165
      Width           =   3000
   End
End
Attribute VB_Name = "frmScreen63"
Attribute VB_GlobalNameSpace = False
Attribute VB_Creatable = False
Attribute VB_PredeclaredId = True
Attribute VB_Exposed = False
' ============================================================
'  frmScreen63  -  reconstructed from Coding.exe (native VB6)
'  Controls above are the REAL names/types from the binary.
'  Layout is placeholder (native VB6 stores no recoverable
'  positions here) - arrange in the IDE. Procedure bodies were
'  not recoverable from native code; add logic as needed.
' ============================================================
