Attribute VB_Name = "modQueries"
' ============================================================================
'  modQueries - the app's real SQL, recovered verbatim from Coding.exe and
'  filtered to genuine statements against the app's own tables. These ARE the
'  data operations the screens perform - the recoverable business logic.
' ============================================================================
Option Explicit

' DELETE agent_clients
Public Const QRY_001 As String = "DELETE FROM agent_clients WHERE agent_serial ="
' DELETE AGENT_CLIENTS
Public Const QRY_002 As String = "delete from AGENT_CLIENTS where CLIENT_NIN ="
' DELETE AGENT_IMAGE
Public Const QRY_003 As String = "delete from AGENT_IMAGE where AGENT_Serial ="
' DELETE Clients_Contact
Public Const QRY_004 As String = "Delete from Clients_Contact where Nin_code ="
' DELETE CLIENTS_TEMP
Public Const QRY_005 As String = "delete from CLIENTS_TEMP where CLIENT_SERIAL = '"
' DELETE clients_temp_edit
Public Const QRY_006 As String = "delete from clients_temp_edit where nin_code ="
' DELETE clientsowner.EGX_CONTACT
Public Const QRY_007 As String = "Delete from clientsowner.EGX_CONTACT"
' INSERT agent_clients_history
Public Const QRY_008 As String = "INSERT INTO agent_clients_history"
' INSERT BROKERS_CONTACT
Public Const QRY_009 As String = "INSERT into BROKERS_CONTACT"
' INSERT Clients_Contact
Public Const QRY_010 As String = "Insert into Clients_Contact (Nin_code ,LAST_BROKER,LAST_User,LANDLINE,MOBILE_PHONE,EMAIL)"
' INSERT Clients_Contact
Public Const QRY_011 As String = "Insert Into Clients_Contact (NIN_CODE,LAST_BROKER,LAST_User,LANDLINE,MOBILE_PHONE,EMAIL)"
' INSERT CLIENTS_REACTIVE
Public Const QRY_012 As String = "INSERT Into CLIENTS_REACTIVE (REQ_ID,NIN_CODE,BROKER_CODE,INSERT_DATE,INSERT_USER,ACTION,ACTION_USER,ACTION_DATE,ACTION_REASON)"
' INSERT clients_temp_edit
Public Const QRY_013 As String = "insert into clients_temp_edit(NIN_CODE, CLIENT_NAME, CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, DATE_OF_BIRTH, CLIENT_ADDRESS, CLIENT_STATUS, INSERT_DATE, GENERATED_DATE, COMPRESSED_NAME, COUNTRY, GOVERNORATE, UPDATE_DATE, BROKER_CODE, INSERT_USER, UPDATE_USER, REMARKS, SUSPEND_RESUME_USER, SUSPEND_RESUME_CODE, RECODING, UPDATE_BROKER_CODE, SUSPEND_RESUME_DATE, INSERT_TEMP_USER, CONFIRM_TEMP_USER, INSERT_TEMP_DATE, CONFIRM_TEMP_DATE,TEMPEDIT_USER,TEMPEDIT_DATE,TEMPEDIT_BROKER, OLD_NIN,ID_ISSUE_DATE ,ID_Expire_DATE ,HUSBAND_NAME, HUSBAND_NAME_comp,Job_Desc ,"
' INSERT CLIENTSOWNER.CLIENTS_REJECTED
Public Const QRY_014 As String = "INSERT INTO CLIENTSOWNER.CLIENTS_REJECTED (CLIENT_SERIAL, BROKER_CODE, CLIENT_NAME, CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, DATE_OF_BIRTH, CLIENT_ADDRESS, INSERT_DATE, COMPRESSED_NAME,COUNTRY, GOVERNORATE, REJECTION_REASON, UPDATE_DATE, OP_TYPE,INSERT_TEMP_USER ,ID_ISSUE_DATE, ID_EXPIRE_DATE, JOB_DESC, HUSBAND_NAME) SELECT nin_code CLIENT_SERIAL, tempedit_broker BROKER_CODE, CLIENT_NAME, CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, DATE_OF_BIRTH, CLIENT_ADDRESS, INSERT_DATE, COMPRESSED_NAME, COUNTRY, GOVERNORAT"
' INSERT clientsowner.EGX_CONTACT
Public Const QRY_015 As String = "Insert Into clientsowner.EGX_CONTACT(ID, NAME, JOB ,LAND_LINE, MOBILE, EMAIL)"
' INSERT coding_bulletins
Public Const QRY_016 As String = "insert into coding_bulletins(BROKER_CODE,BULLETIN_MESSAGE) values('"
' INSERT coding_bulletins
Public Const QRY_017 As String = "insert into coding_bulletins(BROKER_CODE,BULLETIN_MESSAGE, IMAGE_ID) values('"
' INSERT Groups_Permissions2
Public Const QRY_018 As String = "INSERT INTO Groups_Permissions2 VALUES("
' INSERT request_suspend_clients
Public Const QRY_019 As String = "INSERT INTO request_suspend_clients"
' INSERT REQUEST_SUSPEND_CLIENTS
Public Const QRY_020 As String = "Insert into REQUEST_SUSPEND_CLIENTS( CLIENT_SERIAL ,REQUEST_CODE )"
' INSERT REQUEST_SUSPEND_CLIENTS
Public Const QRY_021 As String = "Insert into REQUEST_SUSPEND_CLIENTS( NIN_CODE ,REQUEST_CODE , REQUEST_DATE ,dead_date )"
' INSERT USERS
Public Const QRY_022 As String = "INSERT INTO USERS(GROUP_ID,USER_ID,USERNAME , PASSWORD , BROKER_CODE ,IP_ADDRESS , SUSPEND , IP_RESTRICTION) VALUES("
' INSERT USERS
Public Const QRY_023 As String = "INSERT INTO USERS(GROUP_ID,USER_ID,USERNAME,PASSWORD,IP_ADDRESS , SUSPEND , IP_RESTRICTION) VALUES("
' SELECT BROKERS
Public Const QRY_024 As String = "select (select min(BROKERS.BROKER_NAME_ARB) from BROKERS where BROKER_CODE like '"
' SELECT CODING_BULLETINS
Public Const QRY_025 As String = "select * from (select bulletin_code, 'To:'||decode(broker_code,'0','All',broker_code)||' -> '||to_char(entry_date,'dd/mm/yyyy hh:mi:ss AM') ||' -> '|| bulletin_message bulletin_message , nvl(image_id,-1) image_id from CODING_BULLETINS where entry_date >= sysdate -15 and (broker_code like '0' or broker_code like decode('"
' SELECT AGENT_IMAGE
Public Const QRY_026 As String = "Select * from AGENT_IMAGE where AGENT_Serial =-1"
' SELECT BROKERS_CONTACT
Public Const QRY_027 As String = "Select * from BROKERS_CONTACT"
' SELECT Clients_Contact
Public Const QRY_028 As String = "Select * from Clients_Contact"
' SELECT CLIENTS_NAME_CHANGE_IMAGE
Public Const QRY_029 As String = "Select * from CLIENTS_NAME_CHANGE_IMAGE where ID_SEQ =-1"
' SELECT clientsowner.EGX_CONTACT
Public Const QRY_030 As String = "select * from clientsowner.EGX_CONTACT order by ID"
' SELECT CODING_SYSTEM_VERSION
Public Const QRY_031 As String = "SELECT * FROM CODING_SYSTEM_VERSION"
' SELECT TYPE_TABLE
Public Const QRY_032 As String = "SELECT * FROM TYPE_TABLE WHERE MAIN_SUB = 'M') A,TYPE_TABLE B"
' SELECT BROKERS_CONTACT
Public Const QRY_033 As String = "select 0 from BROKERS_CONTACT where BROKER_CODE ='"
' SELECT AGENT_CLIENTS
Public Const QRY_034 As String = "select 1 from AGENT_CLIENTS"
' SELECT CLIENTSOWNER.clients
Public Const QRY_035 As String = "select A.CLIENT_STATUS + B.CLIENT_STATUS from CLIENTSOWNER.clients A, clients_temp_edit B"
' SELECT agent_clients
Public Const QRY_036 As String = "select Agent_serial from agent_clients B where B.Client_nin = a.nin_code and b.Broker_code ='"
' SELECT brokers_status
Public Const QRY_037 As String = "select B.DOC_IMAGE from brokers_status A , brokers_status_image B where A.broker_code= b.broker_code and Insert_date between START_SUSPEND_DATE and START_SUSPEND_DATE + (1/24/60)/2 and rownum = 1 and A.BROKER_CODE ='"
' SELECT clients
Public Const QRY_038 As String = "select broker_code from clients"
' SELECT BROKERS_VIEW
Public Const QRY_039 As String = "SELECT BROKER_CODE, BROKER_CODE || ' - ' || BROKER_NAME_ARB FROM BROKERS_VIEW WHERE BROKER_CODE LIKE '"
' SELECT brokers
Public Const QRY_040 As String = "select BROKER_CODE, BROKER_CODE || ' - ' || ltrim(rtrim(BROKER_NAME_ARB)) from brokers where (broker_code in (select decode(t1.broker_code,null,t2.BROKER_CODE,t1.BROKER_CODE) broker_code from BROKERS_STATUS t1, brokers t2 where t1.BROKER_CODE(+) = t2.BROKER_CODE and (t1.BROKER_STATUS = 0 or t1.BROKER_CODE is null)) or 6 ="
' SELECT brokers
Public Const QRY_041 As String = "select BROKER_CODE, BROKER_CODE || ' - ' || ltrim(rtrim(BROKER_NAME_ARB)) from brokers where broker_code like '"
' SELECT BROKERS
Public Const QRY_042 As String = "select broker_code,broker_code || ' - ' || broker_name_arb from BROKERS order by 1"
' SELECT BROKERS
Public Const QRY_043 As String = "select broker_code,broker_code||' - '||broker_name_arb broker_name_arb from BROKERS t1 order by broker_code"
' SELECT BROKERS_STATUS_IMAGE
Public Const QRY_044 As String = "SELECT BROKER_CODE,DOC_IMAGE,ACTION_ID FROM BROKERS_STATUS_IMAGE where Broker_Code = '0'"
' SELECT BROKERS
Public Const QRY_045 As String = "Select BROKER_NAME_ARB from BROKERS"
' SELECT BROKERS_STATUS
Public Const QRY_046 As String = "select broker_status, START_SUSPEND_DATE, NVL(ACTION_TYPE,' ') ,nvl(STATUS_COMMENT,' ') from BROKERS_STATUS where broker_code ="
' SELECT ACTIVATION_DEACTIVATION_STATUS
Public Const QRY_047 As String = "SELECT CAN_ACCEPT , Coding_Can_Suspend FROM ACTIVATION_DEACTIVATION_STATUS"
' SELECT cities
Public Const QRY_048 As String = "select city_CODE, city_CODE || ' - ' || ltrim(rtrim(city_DESCRIPTION)) from cities order by city_order,city_DESCRIPTION"
' SELECT cities
Public Const QRY_049 As String = "select city_CODE, city_CODE || ' - ' || ltrim(rtrim(city_DESCRIPTION)) from cities where city_code like decode('"
' SELECT REJECTED_CLIENTS_VIEW
Public Const QRY_050 As String = "select CLIENT_NAME, REJECTION_REASON, UPDATE_DATE, OP_Type From REJECTED_CLIENTS_VIEW where ((SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3 WHERE t2.GROUP_ID = t3.GROUP_ID AND t2.user_id = INSERT_TEMP_USER) LIKE '%3%' OR (SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3 WHERE t2.GROUP_ID = t3.GROUP_ID AND t2.user_id = INSERT_TEMP_USER) LIKE '%4%') AND broker_code = '"
' SELECT clients
Public Const QRY_051 As String = "select client_name,REJECT_TYPE_DESCRIPTION from clients t1,REJECTED_TYPES t2 where t1.CLIENT_STATUS = t2.REJECT_TYPE_CODE(+) and nin_code = '"
' SELECT CLIENTS_NAME_CHANGE
Public Const QRY_052 As String = "SELECT CLIENT_NAME_OLD || CHR (10) || UPDATE_DATE FROM CLIENTS_NAME_CHANGE C WHERE T1.NIN_CODE = C.NIN_CODE AND C.ID_SEQ = (SELECT MAX (ID_SEQ) FROM CLIENTS_NAME_CHANGE B WHERE ACTION_TYPE = 'A' AND C.NIN_CODE = B.nin_code)) CHANGE_NAME"
' SELECT agent_clients
Public Const QRY_053 As String = "select CLIENT_NIN from agent_clients where CLIENT_NIN is not null"
' SELECT REJECTED_CLIENTS_VIEW
Public Const QRY_054 As String = "select CLIENT_SERIAL, BROKER_CODE, CLIENT_NAME, DATE_OF_BIRTH, CLIENT_NATIONALITY, ID_TYPE, ID_NUMBER, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, CLIENT_ADDRESS,REJECTION_REASON, UPDATE_DATE, OP_Type From REJECTED_CLIENTS_VIEW where ((SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3 WHERE t2.GROUP_ID = t3.GROUP_ID AND t2.user_id = INSERT_TEMP_USER) LIKE '%3%' OR (SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3 WHERE t2.GROUP_ID = t3.GROUP_ID AND t2.user_id = INSERT_TEMP_USER) LIKE '%4%') AND broker_code = '"
' SELECT DUAL
Public Const QRY_055 As String = "SELECT clientsowner.BROKER_SUSPEND_SEQ.NEXTVAL FROM DUAL"
' SELECT CLIENTS_REACTIVE
Public Const QRY_056 As String = "SELECT COUNT (0) FROM CLIENTS_REACTIVE R WHERE T1.NIN_CODE = r.NIN_CODE AND ACTION IS NULL ) HaveActiveRequest ,"
' SELECT REQUEST_SUSPEND_CLIENTS
Public Const QRY_057 As String = "Select count (0) from REQUEST_SUSPEND_CLIENTS"
' SELECT REQUEST_SUSPEND_CLIENTS
Public Const QRY_058 As String = "Select count (0) from REQUEST_SUSPEND_CLIENTS where REQUEST_CODE = 3 and REQUEST_EXECUTED is null and nin_code ="
' SELECT clients
Public Const QRY_059 As String = "select count(0) from clients where clients.SUSPEND_RESUME_CODE = 51 AND clients.CLIENT_STATUS = 1 and CLIENTS_REACTIVE.nin_code = clients.Nin_code) ; End;"
' SELECT Clients_Contact
Public Const QRY_060 As String = "select count(0) from Clients_Contact"
' SELECT CLIENTS_REACTIVE
Public Const QRY_061 As String = "select count(0) from CLIENTS_REACTIVE where nin_code="
' SELECT request_suspend_clients
Public Const QRY_062 As String = "select count(0) from request_suspend_clients"
' SELECT ALL_CLIENTS
Public Const QRY_063 As String = "SELECT count('X') FROM ALL_CLIENTS WHERE COMPRESSED_NAME LIKE '"
' SELECT CLIENTS_VIEW
Public Const QRY_064 As String = "SELECT count('X') FROM CLIENTS_VIEW T1 WHERE"
' SELECT CLIENTS_VIEW_LIVE_TEMP
Public Const QRY_065 As String = "SELECT count('X') FROM CLIENTS_VIEW_LIVE_TEMP T1 WHERE"
' SELECT SUSPEND_RESUME_IMAGE
Public Const QRY_066 As String = "select count('x') ImagesCount from SUSPEND_RESUME_IMAGE where nvl(CANCEL,0) = 0 and nin_code ="
' SELECT BROKERS_STATUS
Public Const QRY_067 As String = "select decode(t1.broker_code,null,t2.BROKER_CODE,t1.BROKER_CODE) broker_code from BROKERS_STATUS t1, brokers t2 where t1.BROKER_CODE(+) = t2.BROKER_CODE and (t1.BROKER_STATUS = 0 or t1.BROKER_CODE is null)) ) order by 1"
' SELECT REQUEST_SUSPEND_CLIENTS
Public Const QRY_068 As String = "select distinct 1 from REQUEST_SUSPEND_CLIENTS RSC where t1.nin_code = RSC.NIN_CODE and RSC.REQUEST_EXECUTED is null and RSC.REQUEST_CODE in(2,3)),0) Reserve_suspend"
' SELECT brokers
Public Const QRY_069 As String = "select Distinct brokers.BROKER_CODE, brokers.BROKER_CODE || ' - ' || ltrim(rtrim(BROKER_NAME_ARB)) from brokers , CLIENTS_REACTIVE where"
' SELECT clients
Public Const QRY_070 As String = "select distinct client_name from clients where nin_code = TEMP_CLIENTS_VIEW2.old_nin)"
' SELECT BROKER_CLIENTS
Public Const QRY_071 As String = "SELECT distinct CODE_12 FROM BROKER_CLIENTS WHERE BROKER_CODE = '"
' SELECT BROKERS_COSTS_FINAL
Public Const QRY_072 As String = "select distinct month_cost from BROKERS_COSTS_FINAL where broker_code like '"
' SELECT PERMISSIONS_VIEW
Public Const QRY_073 As String = "select distinct parent_name from PERMISSIONS_VIEW where user_id ="
' SELECT TEMP_CLIENTS_VIEW
Public Const QRY_074 As String = "SELECT DISTINCT T1.BROKER_CODE, T1.BROKER_CODE || ' - ' || T2.BROKER_NAME_ARB FROM TEMP_CLIENTS_VIEW T1, brokers T2 WHERE T1.BROKER_CODE = T2.BROKER_CODE and t1.broker_code like '"
' SELECT TEMP_CLIENTS_VIEW
Public Const QRY_075 As String = "SELECT DISTINCT T1.BROKER_CODE, T1.BROKER_CODE || ' - ' || T2.BROKER_NAME_ARB FROM TEMP_CLIENTS_VIEW T1, brokers T2 WHERE T1.OLD_NIN <> 0 AND T1.BROKER_CODE = T2.BROKER_CODE and t1.DATA_CONFIRM = 1"
' SELECT TEMP_CLIENTS_VIEW
Public Const QRY_076 As String = "SELECT DISTINCT T1.BROKER_CODE,CASE WHEN T3.ROUTING_ID IS NOT NULL THEN T3.ROUTING_ID || ' - ' ELSE '' END || T1.BROKER_CODE || ' - ' || T2.BROKER_NAME_ARB FROM TEMP_CLIENTS_VIEW T1, brokers T2 , CLIENT_REVIEW_ROUTING T3 WHERE"
' SELECT TEMP_CLIENTS_VIEW
Public Const QRY_077 As String = "SELECT DISTINCT T1.BROKER_CODE,CASE WHEN T3.ROUTING_ID IS NOT NULL THEN T3.ROUTING_ID || ' - ' ELSE '' END || T1.BROKER_CODE || ' - ' || T2.BROKER_NAME_ARB FROM TEMP_CLIENTS_VIEW T1, brokers T2 , CLIENT_REVIEW_ROUTING T3 WHERE T1.CLIENT_SERIAL = T3.CLIENT_SERIAL (+) AND T1.OLD_NIN = 0 AND T1.BROKER_CODE = T2.BROKER_CODE and t1.DATA_CONFIRM = 1"
' SELECT CLIENTS_TEMP_EDIT
Public Const QRY_078 As String = "SELECT DISTINCT T1.TEMPEDIT_BROKER, T1.TEMPEDIT_BROKER || ' - ' || T2.BROKER_NAME_ARB FROM CLIENTS_TEMP_EDIT T1, brokers T2 WHERE T1.TEMPEDIT_BROKER = T2.BROKER_CODE"
' SELECT BROKER_CLIENTS
Public Const QRY_079 As String = "SELECT distinct 'مسحوب' FROM BROKER_CLIENTS WHERE BROKER_CODE = '"
' SELECT Agent_IMAGE
Public Const QRY_080 As String = "select File_Doc, FILE_NUMBER from Agent_IMAGE where Delete_USER is null and Agent_Serial ="
' SELECT gender
Public Const QRY_081 As String = "select GENDER_CODE, GENDER_CODE || ' - ' || ltrim(rtrim(GENDER_DESCRIPTION)) from gender"
' SELECT GROUPS
Public Const QRY_082 As String = "SELECT GROUP_ID, GROUP_NAME From GROUPS ORDER BY TO_NUMBER(GROUP_ID)"
' SELECT GROUPS
Public Const QRY_083 As String = "select GROUP_ID, GROUP_NAME from GROUPS where suspend = 0"
' SELECT GROUPS
Public Const QRY_084 As String = "select group_id,group_name from GROUPS"
' SELECT ID_TABLE
Public Const QRY_085 As String = "select I.ID_TYPE, I.ID_TYPE || ' - ' || ltrim(rtrim(I.ID_DESCRIPTION)) , I.VALIDITY_PERIOD , L.MAX_EXPIRY from ID_TABLE I, ID_EXPIRY_DATE_LIMIT L where L.Id_Type=I.Id_Type and (NVL(ALLOWED_GROUPS,'"
' SELECT ID_TABLE
Public Const QRY_086 As String = "select ID_DESCRIPTION from ID_TABLE where ALL_CLIENTS.ID_TYPE = ID_TABLE.ID_TYPE)"
' SELECT ID_TABLE
Public Const QRY_087 As String = "select ID_TYPE, ID_TYPE || ' - ' || ltrim(rtrim(ID_DESCRIPTION)) from ID_TABLE"
' SELECT ID_TABLE
Public Const QRY_088 As String = "select ID_TYPE, ID_TYPE || ' - ' || ltrim(rtrim(ID_DESCRIPTION)) from ID_TABLE where gender_codes like '%"
' SELECT CODING_BULLETINS_IMAGE
Public Const QRY_089 As String = "SELECT IMAGE ,type FROM CODING_BULLETINS_IMAGE WHERE IMAGE_ID ="
' SELECT CODING_BULLETINS_IMAGE
Public Const QRY_090 As String = "SELECT IMAGE_ID,IMAGE,TYPE FROM CODING_BULLETINS_IMAGE where IMAGE_ID= -1"
' SELECT brokers_screens
Public Const QRY_091 As String = "SELECT IP_ADDRESS,START_WORK_DATE,ENABLED_DATE,DISABLE_DATE, DISABLE_DATE from brokers_screens"
' SELECT Clients_Contact
Public Const QRY_092 As String = "Select Last_Broker from Clients_Contact"
' SELECT Clients_Contact
Public Const QRY_093 As String = "select max(UPDATE_DATE) from Clients_Contact"
' SELECT broker_clients
Public Const QRY_094 As String = "select min(code_12) from broker_clients"
' SELECT nationality
Public Const QRY_095 As String = "select NAT_CODE, NAT_CODE || ' - ' || ltrim(rtrim(NAT_DESCRIPTION)) from nationality order by nat_order,NAT_DESCRIPTION"
' SELECT INACTIVE_CLIENTS
Public Const QRY_096 As String = "select nin_code from ( select nin_code from INACTIVE_CLIENTS union select nin_code from request_suspend_clients_view )) a, broker_clients b, brokers c"
' SELECT BROKER_CLIENTS
Public Const QRY_097 As String = "select NIN_CODE from BROKER_CLIENTS where CODE_12 = '"
' SELECT clients
Public Const QRY_098 As String = "select nin_code from clients where SUSPEND_RESUME_CODE =51 and CLIENT_STATUS=1 and nin_code in("
' SELECT BROKER_CLIENTS_VIEW
Public Const QRY_099 As String = "select NIN_CODE, CLIENT_NAME, BROKER_CODE, BROKER_NAME, CODE_12, CODE_10, GENERATED_DATE, GENERATE_USER, INSERT_USER, INSERT_TEMP_USER, CONFIRM_TEMP_USER from BROKER_CLIENTS_VIEW where broker_code like '"
' SELECT BROKER_CLIENTS
Public Const QRY_100 As String = "SELECT NIN_CODE, CLIENT_NAME, CLIENT_NATIONALITY, CLIENT_ADDRESS, to_char(to_date(DATE_OF_BIRTH,'dd/mm/yyyy'),'yyyy/mm/dd') BIRTHDATE, ID_TYPE, ID_NUMBER, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, (SELECT distinct 'مسحوب' FROM BROKER_CLIENTS WHERE BROKER_CODE = '"
' SELECT SUSPEND_RESUME_IMAGE
Public Const QRY_101 As String = "SELECT NIN_CODE, SR_IMAGE, SR_TYPE, FILE_CHKSUM, FILE_NUMBER, REMARKS, BROKER_CODE, USER_ID , scan_date FROM SUSPEND_RESUME_IMAGE WHERE (NIN_CODE = 0)"
' SELECT GROUPS
Public Const QRY_102 As String = "select nvl((select transtime from GROUPS t1, Users t2"
' SELECT GROUPS
Public Const QRY_103 As String = "select nvl((select transtime from GROUPS t1, Users t2 where t1.group_id = t2.group_id and t2.user_id ="
' SELECT CODING_BULLETINS_IMAGE
Public Const QRY_104 As String = "SELECT NVL(MAX(IMAGE_ID),0) +1 IMAGE_ID FROM CODING_BULLETINS_IMAGE"
' SELECT USERS
Public Const QRY_105 As String = "SELECT NVL(MAX(TO_NUMBER(USER_ID)),0) + 1 FROM USERS"
' SELECT GROUPS_PERMISSION_VIEW
Public Const QRY_106 As String = "select OBJECT_ID, OBJECT_NAME, DESCRIPTION, PER_VIEW, PER_ADD, PER_EDIT, PER_DELETE, PER_F01, PER_F02, PER_F03, PER_F04, PER_F05, PER_F06, PER_F07, PER_F08, PER_F09, PER_F10, PER_F11, PER_F12, PER_SCAN from GROUPS_PERMISSION_VIEW where group_id ="
' SELECT PENDING_CODES
Public Const QRY_107 As String = "SELECT OP_TYPE, CLIENT_NAME, ENTRY_DATE, BROKER_CODE,STATUS FROM PENDING_CODES where broker_code = '"
' SELECT SUSPEND_RESUME_REASONS
Public Const QRY_108 As String = "select REASON_CODE, REASON_DESCRIPTION from SUSPEND_RESUME_REASONS where SUSPEND_RESUME_FLAG = '"
' SELECT SUSPEND_RESUME_REASONS
Public Const QRY_109 As String = "select REASON_CODE, REASON_DESCRIPTION from SUSPEND_RESUME_REASONS where SUSPEND_RESUME_FLAG = 'R'"
' SELECT SUSPEND_RESUME_REASONS
Public Const QRY_110 As String = "select REASON_CODE, REASON_DESCRIPTION from SUSPEND_RESUME_REASONS where SUSPEND_RESUME_FLAG = 'S'"
' SELECT REJECTED_TYPES
Public Const QRY_111 As String = "select REJECT_TYPE_CODE, REJECT_TYPE_CODE || ' - ' || REJECT_TYPE_DESCRIPTION from REJECTED_TYPES union select '0' , '0 - غير موقوف' from dual order by 1"
' SELECT REJECTED_TYPES
Public Const QRY_112 As String = "select REJECT_TYPE_CODE, REJECT_TYPE_DESCRIPTION from REJECTED_TYPES"
' SELECT REJECTED_TYPES
Public Const QRY_113 As String = "SELECT REJECT_TYPE_DESCRIPTION FROM REJECTED_TYPES"
' SELECT REJECTED_TYPES
Public Const QRY_114 As String = "SELECT REJECT_TYPE_DESCRIPTION FROM REJECTED_TYPES WHERE REJECT_TYPE_CODE = CLIENT_STATUS),'غير موقوف') CLIENT_STATUS,"
' SELECT REJECTED_TYPES
Public Const QRY_115 As String = "SELECT REJECT_TYPE_DESCRIPTION FROM REJECTED_TYPES WHERE REJECT_TYPE_CODE = CLIENT_STATUS),'غير موقوف') CLIENT_STATUS2,"
' SELECT CLIENTS_REACTIVE
Public Const QRY_116 As String = "select REQUEST_DOC from CLIENTS_REACTIVE where"
' SELECT CLIENTS_REACTIVE
Public Const QRY_117 As String = "SELECT REQUEST_DOC FROM CLIENTS_REACTIVE WHERE nin_code = 10010100"
' SELECT REJECT_REASONS
Public Const QRY_118 As String = "select rownum,reject_desc from REJECT_REASONS"
' SELECT SUSPEND_RESUME_IMAGE
Public Const QRY_119 As String = "select SR_IMAGE, FILE_CHKSUM, FILE_NUMBER, SR_TYPE, REMARKS , scan_date , USERS.USERNAME from SUSPEND_RESUME_IMAGE ,"
' SELECT BROKERS_STATUS
Public Const QRY_120 As String = "select t1.broker_code,t1.broker_code || ' - ' || broker_name_arb from BROKERS_STATUS t1,BROKERS t2 where t1.BROKER_CODE = t2.BROKER_CODE order by 1"
' SELECT BROKER_CLIENTS
Public Const QRY_121 As String = "SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE ,t1.insert_date, 'إعادة تكويد' OP_TYPE FROM CLIENTS_VIEW_all t1 WHERE nvl(t1.update_user,0) <> 'SYSTEM' and t1.broker_code = '"
' SELECT BROKER_CLIENTS
Public Const QRY_122 As String = "SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE, t1.insert_date , 'تسجيل' OP_TYPE FROM CLIENTS_VIEW t1 WHERE nvl(t1.UPDATE_USER,0) <> 10 and t1.broker_code = '"
' SELECT BROKER_CLIENTS
Public Const QRY_123 As String = "SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.insert_date,to_char(to_date('"
' SELECT BROKER_CLIENTS
Public Const QRY_124 As String = "SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.update_broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, (SELECT NAT_DESCRIPTION FROM NATIONALITY WHERE t1.CLIENT_NATIONALITY = NAT_CODE) CLIENT_NATIONALITY, (SELECT ID_DESCRIPTION FROM ID_TABLE WHERE T1.ID_TYPE = ID_TABLE.ID_TYPE) ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE, t1.update_date insert_date , 'تعديل' OP_TYPE FROM CLIENTS_UNION_CLIENTSHISTORY t1 WHERE nvl(t1.update_user,0) <> 10 and t1.update_broker_code = '"
' SELECT BROKER_CLIENTS
Public Const QRY_125 As String = "SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.update_broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, (SELECT NAT_DESCRIPTION FROM NATIONALITY WHERE t1.CLIENT_NATIONALITY = NAT_CODE) CLIENT_NATIONALITY, (SELECT ID_DESCRIPTION FROM ID_TABLE WHERE T1.ID_TYPE = ID_TABLE.ID_TYPE) ID_TYPE, t1.ID_NUMBER, t1.update_date insert_date,to_char(to_date('"
' SELECT BROKER_CLIENTS
Public Const QRY_126 As String = "SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t3.broker_code AND t2.nin_code = t3.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE , t3.Action_date Insert_Date , 'تنشيط الراكد' OP_TYPE FROM CLIENTS_VIEW t1 ,CLIENTS_REACTIVE t3 WHERE t1.NIn_code =t3.Nin_code and t3.Action = 'A' and t3.broker_code = '"
' SELECT BROKER_CLIENTS
Public Const QRY_127 As String = "SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t3.broker_code AND t2.nin_code = t3.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t3.Action_date Insert_Date ,to_char(to_date('"
' SELECT CLIENTS_VIEW
Public Const QRY_128 As String = "SELECT t1.NIN_CODE, CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE, T2.GENERATED_DATE insert_date, 'سحب' OP_TYPE FROM CLIENTS_VIEW T1, BROKER_CLIENTS T2 WHERE nvl(t1.UPDATE_USER,0) <> 10 and to_char(T2.generated_date,'yyyymmdd') between '"
' SELECT CLIENTS_VIEW
Public Const QRY_129 As String = "SELECT t1.NIN_CODE, CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, T2.GENERATED_DATE insert_date,to_char(to_date('20070801','yyyymmdd'),'dd/mm/yyyy') DATEFROM, to_char(to_date('20070901','yyyymmdd'),'dd/mm/yyyy') DATETO, 'سحب' OP_TYPE ,t1.ID_EXPIRE_DATE FROM CLIENTS_VIEW T1, BROKER_CLIENTS T2 WHERE to_char(T2.generated_date,'yyyymmdd') between '"
' SELECT USERS
Public Const QRY_130 As String = "SELECT t3.GROUP_DATA_ACCESS FROM USERS t2,GROUPS t3"
' SELECT USERS
Public Const QRY_131 As String = "SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3"
' SELECT clients_temp_edit
Public Const QRY_132 As String = "select TEMPEDIT_BROKER from clients_temp_edit"
' SELECT GROUPS
Public Const QRY_133 As String = "select transtime from GROUPS where group_id ="
' SELECT BROKERS_COSTS_FINAL
Public Const QRY_134 As String = "SELECT tYPE,FEES FROM BROKERS_COSTS_FINAL WHERE MONTH_COST = '"
' SELECT TYPE_Table
Public Const QRY_135 As String = "select TYPE_CODE , FUND_CATEGORY from TYPE_Table"
' SELECT TYPE_TABLE
Public Const QRY_136 As String = "select TYPE_CODE, TYPE_CODE || ' - ' || ltrim(rtrim(TYPE_DESCRIPTION)) from TYPE_TABLE where main_sub = 'M' and gender_codes like '%"
' SELECT TYPE_TABLE
Public Const QRY_137 As String = "select TYPE_CODE, TYPE_CODE ||' - '||ltrim(rtrim(TYPE_DESCRIPTION)) from TYPE_TABLE where main_sub = 'S' and type_code like '"
' SELECT broker_clients
Public Const QRY_138 As String = "select 'X' from broker_clients BC where bc.NIN_CODE = t1.nin_code and bc.BROKER_CODE = '"
' SELECT clients
Public Const QRY_139 As String = "select 'x' from clients t8 where t8.nin_code = t1.nin_code and (CLIENT_MAIN_TYPE in ('8','6') or CLIENT_SUB_TYPE in ('80','60'))) and NIN_CODE ="
' SELECT clients
Public Const QRY_140 As String = "select 'x' from clients T8 where T8.nin_code = t1.nin_code and client_main_type in (3,4))"
' SELECT CLIENTS_TEMP_EDIT
Public Const QRY_141 As String = "Select 'x' FROM CLIENTS_TEMP_EDIT T2 WHERE T2.nin_code ="
' SELECT PERMISSIONS_VIEW
Public Const QRY_142 As String = "SELECT 'X' FROM PERMISSIONS_VIEW WHERE USER_ID ="
' SELECT CLIENTS_TEMP
Public Const QRY_143 As String = "select 'Y' from CLIENTS_TEMP T7 where"
' SELECT CLIENTS_TEMP
Public Const QRY_144 As String = "select 'Y' from CLIENTS_TEMP T7 where t7.old_NIN = t1.nin_code),'N')||"
' SELECT CLIENTS_TEMP_EDIT
Public Const QRY_145 As String = "select 'Y' from CLIENTS_TEMP_EDIT T6"
' SELECT CLIENTS_TEMP_EDIT
Public Const QRY_146 As String = "select 'Y' from CLIENTS_TEMP_EDIT T6 where t6.NIN_CODE = t1.nin_code),'N') RECODING_EDITING,"
' SELECT CLIENTS_TEMP_EDIT
Public Const QRY_147 As String = "select 'Y'||TEMPEDIT_BROKER from CLIENTS_TEMP_EDIT T6"
' UPDATE AGENT_CLIENTS
Public Const QRY_148 As String = "UPDATE AGENT_CLIENTS set"
' UPDATE AGENT_CLIENTS
Public Const QRY_149 As String = "Update AGENT_CLIENTS set AGENT_NIN="
' UPDATE AGENT_CLIENTS
Public Const QRY_150 As String = "update AGENT_CLIENTS set CLIENT_NIN = CLIENT_NIN-1000000000 where BROKER_CODE ='"
' UPDATE AGENT_IMAGE
Public Const QRY_151 As String = "Update AGENT_IMAGE set"
' UPDATE BROKERS_CONTACT
Public Const QRY_152 As String = "UPDATE BROKERS_CONTACT SET"
' UPDATE BROKERS_STATUS
Public Const QRY_153 As String = "update BROKERS_STATUS set broker_status = '"
' UPDATE clients
Public Const QRY_154 As String = "update clients set CLIENT_STATUS = '"
' UPDATE clients
Public Const QRY_155 As String = "update clients set CLIENT_STATUS = '0', SUSPEND_RESUME_CODE = '"
' UPDATE clients
Public Const QRY_156 As String = "update clients set CLIENT_STATUS = '4', SUSPEND_RESUME_CODE = '4', SUSPEND_RESUME_USER = '"
' UPDATE CLIENTS
Public Const QRY_157 As String = "UPDATE CLIENTS SET(CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, DATE_OF_BIRTH, CLIENT_ADDRESS, COUNTRY, GOVERNORATE, UPDATE_DATE, UPDATE_BROKER_CODE, UPDATE_USER ,ID_ISSUE_DATE , ID_EXPIRE_DATE ,HUSBAND_NAME ,HUSBAND_NAME_comp , Job_Desc , FUND_TYPE ) ="
' UPDATE CLIENTS
Public Const QRY_158 As String = "UPDATE CLIENTS T1 SET(T1.CLIENT_GENDER, T1.ID_TYPE, T1.ID_NUMBER, T1.CLIENT_NATIONALITY, T1.CLIENT_MAIN_TYPE, T1.CLIENT_SUB_TYPE, T1.DATE_OF_BIRTH, T1.CLIENT_ADDRESS, T1.COUNTRY, T1.GOVERNORATE, T1.UPDATE_DATE, T1.UPDATE_BROKER_CODE, T1.UPDATE_USER, T1.TEMPEDIT_USER, T1.TEMPEDIT_DATE, T1.TEMPEDIT_BROKER ,T1.ID_ISSUE_DATE, T1.ID_EXPIRE_DATE, T1.JOB_DESC, T1.HUSBAND_NAME ,T1.HUSBAND_NAME_comp ,T1.FUND_TYPE) ="
' UPDATE Clients_Contact
Public Const QRY_159 As String = "Update Clients_Contact set (LAST_BROKER,LAST_User,LANDLINE,MOBILE_PHONE,EMAIL)"
' UPDATE Clients_Contact
Public Const QRY_160 As String = "Update Clients_Contact set (LAST_User,LANDLINE,MOBILE_PHONE,EMAIL , UPDATE_DATE)"
' UPDATE clients_contact
Public Const QRY_161 As String = "Update clients_contact set nin_code ="
' UPDATE CLIENTS_contact
Public Const QRY_162 As String = "update CLIENTS_contact set(LAST_BROKER,LAST_User,LANDLINE,MOBILE_PHONE,EMAIL , UPDATE_DATE)="
' UPDATE CLIENTS_REACTIVE
Public Const QRY_163 As String = "UPDATE CLIENTS_REACTIVE SET ACTION = 'A',ACTION_USER ="
' UPDATE CLIENTS_REACTIVE
Public Const QRY_164 As String = "update CLIENTS_REACTIVE set ACTION ='R' , ACTION_DATE =sysdate , ACTION_user="
' UPDATE CLIENTS_TEMP
Public Const QRY_165 As String = "update CLIENTS_TEMP set confirm_Date = Sysdate , data_confirm = '1',confirm_temp_user = '"
' UPDATE clients_temp_edit
Public Const QRY_166 As String = "update clients_temp_edit set (CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY,"
' UPDATE CLIENTS_TEMP_EDIT
Public Const QRY_167 As String = "update CLIENTS_TEMP_EDIT set CLIENT_STATUS = '"
' UPDATE clients_Temp_Edit
Public Const QRY_168 As String = "update clients_Temp_Edit set CLIENT_STATUS = '0', SUSPEND_RESUME_CODE = '"
' UPDATE CLIENTSOWNER.brokers_screens
Public Const QRY_169 As String = "UPDATE CLIENTSOWNER.brokers_screens SET STATUS =0 , ENABLED_Date = sysdate,Disable_Date =NULL where Broker_code='"
' UPDATE CLIENTSOWNER.brokers_screens
Public Const QRY_170 As String = "UPDATE CLIENTSOWNER.brokers_screens SET STATUS =2 , Disable_Date = sysdate where Broker_code='"
' UPDATE CODING_BULLETINS
Public Const QRY_171 As String = "update CODING_BULLETINS set BULLETIN_MESSAGE = 'تم إغلاق النظام عن طريق إدارة التكويد فى هذا الوقت' where bulletin_code ="
' UPDATE groups
Public Const QRY_172 As String = "update groups set transtime = ltrim(rtrim('"
' UPDATE REQUEST_SUSPEND_CLIENTS
Public Const QRY_173 As String = "Update REQUEST_SUSPEND_CLIENTS set"
' UPDATE REQUEST_SUSPEND_CLIENTS
Public Const QRY_174 As String = "Update REQUEST_SUSPEND_CLIENTS set REQUEST_DATE =trunc(sysdate) ,Dead_DATE ="
' UPDATE SUSPEND_RESUME_IMAGE
Public Const QRY_175 As String = "UPDATE SUSPEND_RESUME_IMAGE set Cancel = 1 , CANCEL_DATE = sysDate Where NIN_CODE ||'--' || To_Char(Scan_Date,'YYYY/MM/DD HH24:MI:SS')||'--'|| File_number in ("
' UPDATE USERS
Public Const QRY_176 As String = "UPDATE USERS SET ip_address = '"
' UPDATE USERS
Public Const QRY_177 As String = "UPDATE USERS SET USERNAME = upper('"
