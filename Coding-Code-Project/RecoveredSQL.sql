DELETE FROM agent_clients WHERE agent_serial =
delete from AGENT_CLIENTS where CLIENT_NIN =
delete from AGENT_IMAGE where AGENT_Serial =
Delete from Clients_Contact where Nin_code =
delete from CLIENTS_TEMP where CLIENT_SERIAL = '
delete from clients_temp_edit where nin_code =
Delete from clientsowner.EGX_CONTACT
INSERT INTO agent_clients_history
INSERT into BROKERS_CONTACT
Insert into Clients_Contact (Nin_code ,LAST_BROKER,LAST_User,LANDLINE,MOBILE_PHONE,EMAIL)
Insert Into Clients_Contact (NIN_CODE,LAST_BROKER,LAST_User,LANDLINE,MOBILE_PHONE,EMAIL)
INSERT Into CLIENTS_REACTIVE (REQ_ID,NIN_CODE,BROKER_CODE,INSERT_DATE,INSERT_USER,ACTION,ACTION_USER,ACTION_DATE,ACTION_REASON)
insert into clients_temp_edit(NIN_CODE, CLIENT_NAME, CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, DATE_OF_BIRTH, CLIENT_ADDRESS, CLIENT_STATUS, INSERT_DATE, GENERATED_DATE, COMPRESSED_NAME, COUNTRY, GOVERNORATE, UPDATE_DATE, BROKER_CODE, INSERT_USER, UPDATE_USER, REMARKS, SUSPEND_RESUME_USER, SUSPEND_RESUME_CODE, RECODING, UPDATE_BROKER_CODE, SUSPEND_RESUME_DATE, INSERT_TEMP_USER, CONFIRM_TEMP_USER, INSERT_TEMP_DATE, CONFIRM_TEMP_DATE,TEMPEDIT_USER,TEMPEDIT_DATE,TEMPEDIT_BROKER, OLD_NIN,ID_ISSUE_DATE ,ID_Expire_DATE ,HUSBAND_NAME, HUSBAND_NAME_comp,Job_Desc ,
INSERT INTO CLIENTSOWNER.CLIENTS_REJECTED (CLIENT_SERIAL, BROKER_CODE, CLIENT_NAME, CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, DATE_OF_BIRTH, CLIENT_ADDRESS, INSERT_DATE, COMPRESSED_NAME,COUNTRY, GOVERNORATE, REJECTION_REASON, UPDATE_DATE, OP_TYPE,INSERT_TEMP_USER ,ID_ISSUE_DATE, ID_EXPIRE_DATE, JOB_DESC, HUSBAND_NAME) SELECT nin_code CLIENT_SERIAL, tempedit_broker BROKER_CODE, CLIENT_NAME, CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, DATE_OF_BIRTH, CLIENT_ADDRESS, INSERT_DATE, COMPRESSED_NAME, COUNTRY, GOVERNORAT
Insert Into clientsowner.EGX_CONTACT(ID, NAME, JOB ,LAND_LINE, MOBILE, EMAIL)
insert into coding_bulletins(BROKER_CODE,BULLETIN_MESSAGE) values('
insert into coding_bulletins(BROKER_CODE,BULLETIN_MESSAGE, IMAGE_ID) values('
INSERT INTO Groups_Permissions2 VALUES(
INSERT INTO request_suspend_clients
Insert into REQUEST_SUSPEND_CLIENTS( CLIENT_SERIAL ,REQUEST_CODE )
Insert into REQUEST_SUSPEND_CLIENTS( NIN_CODE ,REQUEST_CODE , REQUEST_DATE ,dead_date )
INSERT INTO USERS(GROUP_ID,USER_ID,USERNAME , PASSWORD , BROKER_CODE ,IP_ADDRESS , SUSPEND , IP_RESTRICTION) VALUES(
INSERT INTO USERS(GROUP_ID,USER_ID,USERNAME,PASSWORD,IP_ADDRESS , SUSPEND , IP_RESTRICTION) VALUES(
select (select min(BROKERS.BROKER_NAME_ARB) from BROKERS where BROKER_CODE like '
select * from (select bulletin_code, 'To:'||decode(broker_code,'0','All',broker_code)||' -> '||to_char(entry_date,'dd/mm/yyyy hh:mi:ss AM') ||' -> '|| bulletin_message bulletin_message , nvl(image_id,-1) image_id from CODING_BULLETINS where entry_date >= sysdate -15 and (broker_code like '0' or broker_code like decode('
Select * from AGENT_IMAGE where AGENT_Serial =-1
Select * from BROKERS_CONTACT
Select * from Clients_Contact
Select * from CLIENTS_NAME_CHANGE_IMAGE where ID_SEQ =-1
select * from clientsowner.EGX_CONTACT order by ID
SELECT * FROM CODING_SYSTEM_VERSION
SELECT * FROM TYPE_TABLE WHERE MAIN_SUB = 'M') A,TYPE_TABLE B
select 0 from BROKERS_CONTACT where BROKER_CODE ='
select 1 from AGENT_CLIENTS
select A.CLIENT_STATUS + B.CLIENT_STATUS from CLIENTSOWNER.clients A, clients_temp_edit B
select Agent_serial from agent_clients B where B.Client_nin = a.nin_code and b.Broker_code ='
select B.DOC_IMAGE from brokers_status A , brokers_status_image B where A.broker_code= b.broker_code and Insert_date between START_SUSPEND_DATE and START_SUSPEND_DATE + (1/24/60)/2 and rownum = 1 and A.BROKER_CODE ='
select broker_code from clients
SELECT BROKER_CODE, BROKER_CODE || ' - ' || BROKER_NAME_ARB FROM BROKERS_VIEW WHERE BROKER_CODE LIKE '
select BROKER_CODE, BROKER_CODE || ' - ' || ltrim(rtrim(BROKER_NAME_ARB)) from brokers where (broker_code in (select decode(t1.broker_code,null,t2.BROKER_CODE,t1.BROKER_CODE) broker_code from BROKERS_STATUS t1, brokers t2 where t1.BROKER_CODE(+) = t2.BROKER_CODE and (t1.BROKER_STATUS = 0 or t1.BROKER_CODE is null)) or 6 =
select BROKER_CODE, BROKER_CODE || ' - ' || ltrim(rtrim(BROKER_NAME_ARB)) from brokers where broker_code like '
select broker_code,broker_code || ' - ' || broker_name_arb from BROKERS order by 1
select broker_code,broker_code||' - '||broker_name_arb broker_name_arb from BROKERS t1 order by broker_code
SELECT BROKER_CODE,DOC_IMAGE,ACTION_ID FROM BROKERS_STATUS_IMAGE where Broker_Code = '0'
Select BROKER_NAME_ARB from BROKERS
select broker_status, START_SUSPEND_DATE, NVL(ACTION_TYPE,' ') ,nvl(STATUS_COMMENT,' ') from BROKERS_STATUS where broker_code =
SELECT CAN_ACCEPT , Coding_Can_Suspend FROM ACTIVATION_DEACTIVATION_STATUS
select city_CODE, city_CODE || ' - ' || ltrim(rtrim(city_DESCRIPTION)) from cities order by city_order,city_DESCRIPTION
select city_CODE, city_CODE || ' - ' || ltrim(rtrim(city_DESCRIPTION)) from cities where city_code like decode('
select CLIENT_NAME, REJECTION_REASON, UPDATE_DATE, OP_Type From REJECTED_CLIENTS_VIEW where ((SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3 WHERE t2.GROUP_ID = t3.GROUP_ID AND t2.user_id = INSERT_TEMP_USER) LIKE '%3%' OR (SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3 WHERE t2.GROUP_ID = t3.GROUP_ID AND t2.user_id = INSERT_TEMP_USER) LIKE '%4%') AND broker_code = '
select client_name,REJECT_TYPE_DESCRIPTION from clients t1,REJECTED_TYPES t2 where t1.CLIENT_STATUS = t2.REJECT_TYPE_CODE(+) and nin_code = '
SELECT CLIENT_NAME_OLD || CHR (10) || UPDATE_DATE FROM CLIENTS_NAME_CHANGE C WHERE T1.NIN_CODE = C.NIN_CODE AND C.ID_SEQ = (SELECT MAX (ID_SEQ) FROM CLIENTS_NAME_CHANGE B WHERE ACTION_TYPE = 'A' AND C.NIN_CODE = B.nin_code)) CHANGE_NAME
select CLIENT_NIN from agent_clients where CLIENT_NIN is not null
select CLIENT_SERIAL, BROKER_CODE, CLIENT_NAME, DATE_OF_BIRTH, CLIENT_NATIONALITY, ID_TYPE, ID_NUMBER, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, CLIENT_ADDRESS,REJECTION_REASON, UPDATE_DATE, OP_Type From REJECTED_CLIENTS_VIEW where ((SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3 WHERE t2.GROUP_ID = t3.GROUP_ID AND t2.user_id = INSERT_TEMP_USER) LIKE '%3%' OR (SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3 WHERE t2.GROUP_ID = t3.GROUP_ID AND t2.user_id = INSERT_TEMP_USER) LIKE '%4%') AND broker_code = '
SELECT clientsowner.BROKER_SUSPEND_SEQ.NEXTVAL FROM DUAL
SELECT COUNT (0) FROM CLIENTS_REACTIVE R WHERE T1.NIN_CODE = r.NIN_CODE AND ACTION IS NULL ) HaveActiveRequest ,
Select count (0) from REQUEST_SUSPEND_CLIENTS
Select count (0) from REQUEST_SUSPEND_CLIENTS where REQUEST_CODE = 3 and REQUEST_EXECUTED is null and nin_code =
select count(0) from clients where clients.SUSPEND_RESUME_CODE = 51 AND clients.CLIENT_STATUS = 1 and CLIENTS_REACTIVE.nin_code = clients.Nin_code) ; End;
select count(0) from Clients_Contact
select count(0) from CLIENTS_REACTIVE where nin_code=
select count(0) from request_suspend_clients
SELECT count('X') FROM ALL_CLIENTS WHERE COMPRESSED_NAME LIKE '
SELECT count('X') FROM CLIENTS_VIEW T1 WHERE
SELECT count('X') FROM CLIENTS_VIEW_LIVE_TEMP T1 WHERE
select count('x') ImagesCount from SUSPEND_RESUME_IMAGE where nvl(CANCEL,0) = 0 and nin_code =
select decode(t1.broker_code,null,t2.BROKER_CODE,t1.BROKER_CODE) broker_code from BROKERS_STATUS t1, brokers t2 where t1.BROKER_CODE(+) = t2.BROKER_CODE and (t1.BROKER_STATUS = 0 or t1.BROKER_CODE is null)) ) order by 1
select distinct 1 from REQUEST_SUSPEND_CLIENTS RSC where t1.nin_code = RSC.NIN_CODE and RSC.REQUEST_EXECUTED is null and RSC.REQUEST_CODE in(2,3)),0) Reserve_suspend
select Distinct brokers.BROKER_CODE, brokers.BROKER_CODE || ' - ' || ltrim(rtrim(BROKER_NAME_ARB)) from brokers , CLIENTS_REACTIVE where
select distinct client_name from clients where nin_code = TEMP_CLIENTS_VIEW2.old_nin)
SELECT distinct CODE_12 FROM BROKER_CLIENTS WHERE BROKER_CODE = '
select distinct month_cost from BROKERS_COSTS_FINAL where broker_code like '
select distinct parent_name from PERMISSIONS_VIEW where user_id =
SELECT DISTINCT T1.BROKER_CODE, T1.BROKER_CODE || ' - ' || T2.BROKER_NAME_ARB FROM TEMP_CLIENTS_VIEW T1, brokers T2 WHERE T1.BROKER_CODE = T2.BROKER_CODE and t1.broker_code like '
SELECT DISTINCT T1.BROKER_CODE, T1.BROKER_CODE || ' - ' || T2.BROKER_NAME_ARB FROM TEMP_CLIENTS_VIEW T1, brokers T2 WHERE T1.OLD_NIN <> 0 AND T1.BROKER_CODE = T2.BROKER_CODE and t1.DATA_CONFIRM = 1
SELECT DISTINCT T1.BROKER_CODE,CASE WHEN T3.ROUTING_ID IS NOT NULL THEN T3.ROUTING_ID || ' - ' ELSE '' END || T1.BROKER_CODE || ' - ' || T2.BROKER_NAME_ARB FROM TEMP_CLIENTS_VIEW T1, brokers T2 , CLIENT_REVIEW_ROUTING T3 WHERE
SELECT DISTINCT T1.BROKER_CODE,CASE WHEN T3.ROUTING_ID IS NOT NULL THEN T3.ROUTING_ID || ' - ' ELSE '' END || T1.BROKER_CODE || ' - ' || T2.BROKER_NAME_ARB FROM TEMP_CLIENTS_VIEW T1, brokers T2 , CLIENT_REVIEW_ROUTING T3 WHERE T1.CLIENT_SERIAL = T3.CLIENT_SERIAL (+) AND T1.OLD_NIN = 0 AND T1.BROKER_CODE = T2.BROKER_CODE and t1.DATA_CONFIRM = 1
SELECT DISTINCT T1.TEMPEDIT_BROKER, T1.TEMPEDIT_BROKER || ' - ' || T2.BROKER_NAME_ARB FROM CLIENTS_TEMP_EDIT T1, brokers T2 WHERE T1.TEMPEDIT_BROKER = T2.BROKER_CODE
SELECT distinct 'مسحوب' FROM BROKER_CLIENTS WHERE BROKER_CODE = '
select File_Doc, FILE_NUMBER from Agent_IMAGE where Delete_USER is null and Agent_Serial =
select GENDER_CODE, GENDER_CODE || ' - ' || ltrim(rtrim(GENDER_DESCRIPTION)) from gender
SELECT GROUP_ID, GROUP_NAME From GROUPS ORDER BY TO_NUMBER(GROUP_ID)
select GROUP_ID, GROUP_NAME from GROUPS where suspend = 0
select group_id,group_name from GROUPS
select I.ID_TYPE, I.ID_TYPE || ' - ' || ltrim(rtrim(I.ID_DESCRIPTION)) , I.VALIDITY_PERIOD , L.MAX_EXPIRY from ID_TABLE I, ID_EXPIRY_DATE_LIMIT L where L.Id_Type=I.Id_Type and (NVL(ALLOWED_GROUPS,'
select ID_DESCRIPTION from ID_TABLE where ALL_CLIENTS.ID_TYPE = ID_TABLE.ID_TYPE)
select ID_TYPE, ID_TYPE || ' - ' || ltrim(rtrim(ID_DESCRIPTION)) from ID_TABLE
select ID_TYPE, ID_TYPE || ' - ' || ltrim(rtrim(ID_DESCRIPTION)) from ID_TABLE where gender_codes like '%
SELECT IMAGE ,type FROM CODING_BULLETINS_IMAGE WHERE IMAGE_ID =
SELECT IMAGE_ID,IMAGE,TYPE FROM CODING_BULLETINS_IMAGE where IMAGE_ID= -1
SELECT IP_ADDRESS,START_WORK_DATE,ENABLED_DATE,DISABLE_DATE, DISABLE_DATE from brokers_screens
Select Last_Broker from Clients_Contact
select max(UPDATE_DATE) from Clients_Contact
select min(code_12) from broker_clients
select NAT_CODE, NAT_CODE || ' - ' || ltrim(rtrim(NAT_DESCRIPTION)) from nationality order by nat_order,NAT_DESCRIPTION
select nin_code from ( select nin_code from INACTIVE_CLIENTS union select nin_code from request_suspend_clients_view )) a, broker_clients b, brokers c
select NIN_CODE from BROKER_CLIENTS where CODE_12 = '
select nin_code from clients where SUSPEND_RESUME_CODE =51 and CLIENT_STATUS=1 and nin_code in(
select NIN_CODE, CLIENT_NAME, BROKER_CODE, BROKER_NAME, CODE_12, CODE_10, GENERATED_DATE, GENERATE_USER, INSERT_USER, INSERT_TEMP_USER, CONFIRM_TEMP_USER from BROKER_CLIENTS_VIEW where broker_code like '
SELECT NIN_CODE, CLIENT_NAME, CLIENT_NATIONALITY, CLIENT_ADDRESS, to_char(to_date(DATE_OF_BIRTH,'dd/mm/yyyy'),'yyyy/mm/dd') BIRTHDATE, ID_TYPE, ID_NUMBER, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, (SELECT distinct 'مسحوب' FROM BROKER_CLIENTS WHERE BROKER_CODE = '
SELECT NIN_CODE, SR_IMAGE, SR_TYPE, FILE_CHKSUM, FILE_NUMBER, REMARKS, BROKER_CODE, USER_ID , scan_date FROM SUSPEND_RESUME_IMAGE WHERE (NIN_CODE = 0)
select nvl((select transtime from GROUPS t1, Users t2
select nvl((select transtime from GROUPS t1, Users t2 where t1.group_id = t2.group_id and t2.user_id =
SELECT NVL(MAX(IMAGE_ID),0) +1 IMAGE_ID FROM CODING_BULLETINS_IMAGE
SELECT NVL(MAX(TO_NUMBER(USER_ID)),0) + 1 FROM USERS
select OBJECT_ID, OBJECT_NAME, DESCRIPTION, PER_VIEW, PER_ADD, PER_EDIT, PER_DELETE, PER_F01, PER_F02, PER_F03, PER_F04, PER_F05, PER_F06, PER_F07, PER_F08, PER_F09, PER_F10, PER_F11, PER_F12, PER_SCAN from GROUPS_PERMISSION_VIEW where group_id =
SELECT OP_TYPE, CLIENT_NAME, ENTRY_DATE, BROKER_CODE,STATUS FROM PENDING_CODES where broker_code = '
select REASON_CODE, REASON_DESCRIPTION from SUSPEND_RESUME_REASONS where SUSPEND_RESUME_FLAG = '
select REASON_CODE, REASON_DESCRIPTION from SUSPEND_RESUME_REASONS where SUSPEND_RESUME_FLAG = 'R'
select REASON_CODE, REASON_DESCRIPTION from SUSPEND_RESUME_REASONS where SUSPEND_RESUME_FLAG = 'S'
select REJECT_TYPE_CODE, REJECT_TYPE_CODE || ' - ' || REJECT_TYPE_DESCRIPTION from REJECTED_TYPES union select '0' , '0 - غير موقوف' from dual order by 1
select REJECT_TYPE_CODE, REJECT_TYPE_DESCRIPTION from REJECTED_TYPES
SELECT REJECT_TYPE_DESCRIPTION FROM REJECTED_TYPES
SELECT REJECT_TYPE_DESCRIPTION FROM REJECTED_TYPES WHERE REJECT_TYPE_CODE = CLIENT_STATUS),'غير موقوف') CLIENT_STATUS,
SELECT REJECT_TYPE_DESCRIPTION FROM REJECTED_TYPES WHERE REJECT_TYPE_CODE = CLIENT_STATUS),'غير موقوف') CLIENT_STATUS2,
select REQUEST_DOC from CLIENTS_REACTIVE where
SELECT REQUEST_DOC FROM CLIENTS_REACTIVE WHERE nin_code = 10010100
select rownum,reject_desc from REJECT_REASONS
select SR_IMAGE, FILE_CHKSUM, FILE_NUMBER, SR_TYPE, REMARKS , scan_date , USERS.USERNAME from SUSPEND_RESUME_IMAGE ,
select t1.broker_code,t1.broker_code || ' - ' || broker_name_arb from BROKERS_STATUS t1,BROKERS t2 where t1.BROKER_CODE = t2.BROKER_CODE order by 1
SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE ,t1.insert_date, 'إعادة تكويد' OP_TYPE FROM CLIENTS_VIEW_all t1 WHERE nvl(t1.update_user,0) <> 'SYSTEM' and t1.broker_code = '
SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE, t1.insert_date , 'تسجيل' OP_TYPE FROM CLIENTS_VIEW t1 WHERE nvl(t1.UPDATE_USER,0) <> 10 and t1.broker_code = '
SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.insert_date,to_char(to_date('
SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.update_broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, (SELECT NAT_DESCRIPTION FROM NATIONALITY WHERE t1.CLIENT_NATIONALITY = NAT_CODE) CLIENT_NATIONALITY, (SELECT ID_DESCRIPTION FROM ID_TABLE WHERE T1.ID_TYPE = ID_TABLE.ID_TYPE) ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE, t1.update_date insert_date , 'تعديل' OP_TYPE FROM CLIENTS_UNION_CLIENTSHISTORY t1 WHERE nvl(t1.update_user,0) <> 10 and t1.update_broker_code = '
SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t1.update_broker_code AND t2.nin_code = t1.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, (SELECT NAT_DESCRIPTION FROM NATIONALITY WHERE t1.CLIENT_NATIONALITY = NAT_CODE) CLIENT_NATIONALITY, (SELECT ID_DESCRIPTION FROM ID_TABLE WHERE T1.ID_TYPE = ID_TABLE.ID_TYPE) ID_TYPE, t1.ID_NUMBER, t1.update_date insert_date,to_char(to_date('
SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t3.broker_code AND t2.nin_code = t3.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE , t3.Action_date Insert_Date , 'تنشيط الراكد' OP_TYPE FROM CLIENTS_VIEW t1 ,CLIENTS_REACTIVE t3 WHERE t1.NIn_code =t3.Nin_code and t3.Action = 'A' and t3.broker_code = '
SELECT t1.NIN_CODE, (SELECT t2.code_12 FROM BROKER_CLIENTS t2 WHERE t2.broker_code = t3.broker_code AND t2.nin_code = t3.nin_code and substr(t2.code_12,1,1) <> 9) CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t3.Action_date Insert_Date ,to_char(to_date('
SELECT t1.NIN_CODE, CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, t1.ID_EXPIRE_DATE, T2.GENERATED_DATE insert_date, 'سحب' OP_TYPE FROM CLIENTS_VIEW T1, BROKER_CLIENTS T2 WHERE nvl(t1.UPDATE_USER,0) <> 10 and to_char(T2.generated_date,'yyyymmdd') between '
SELECT t1.NIN_CODE, CODE_12, t1.CLIENT_NAME, t1.CLIENT_NATIONALITY, t1.ID_TYPE, t1.ID_NUMBER, T2.GENERATED_DATE insert_date,to_char(to_date('20070801','yyyymmdd'),'dd/mm/yyyy') DATEFROM, to_char(to_date('20070901','yyyymmdd'),'dd/mm/yyyy') DATETO, 'سحب' OP_TYPE ,t1.ID_EXPIRE_DATE FROM CLIENTS_VIEW T1, BROKER_CLIENTS T2 WHERE to_char(T2.generated_date,'yyyymmdd') between '
SELECT t3.GROUP_DATA_ACCESS FROM USERS t2,GROUPS t3
SELECT t3.GROUP_DATA_ACCESS group_id FROM USERS t2,GROUPS t3
select TEMPEDIT_BROKER from clients_temp_edit
select transtime from GROUPS where group_id =
SELECT tYPE,FEES FROM BROKERS_COSTS_FINAL WHERE MONTH_COST = '
select TYPE_CODE , FUND_CATEGORY from TYPE_Table
select TYPE_CODE, TYPE_CODE || ' - ' || ltrim(rtrim(TYPE_DESCRIPTION)) from TYPE_TABLE where main_sub = 'M' and gender_codes like '%
select TYPE_CODE, TYPE_CODE ||' - '||ltrim(rtrim(TYPE_DESCRIPTION)) from TYPE_TABLE where main_sub = 'S' and type_code like '
select 'X' from broker_clients BC where bc.NIN_CODE = t1.nin_code and bc.BROKER_CODE = '
select 'x' from clients t8 where t8.nin_code = t1.nin_code and (CLIENT_MAIN_TYPE in ('8','6') or CLIENT_SUB_TYPE in ('80','60'))) and NIN_CODE =
select 'x' from clients T8 where T8.nin_code = t1.nin_code and client_main_type in (3,4))
Select 'x' FROM CLIENTS_TEMP_EDIT T2 WHERE T2.nin_code =
SELECT 'X' FROM PERMISSIONS_VIEW WHERE USER_ID =
select 'Y' from CLIENTS_TEMP T7 where
select 'Y' from CLIENTS_TEMP T7 where t7.old_NIN = t1.nin_code),'N')||
select 'Y' from CLIENTS_TEMP_EDIT T6
select 'Y' from CLIENTS_TEMP_EDIT T6 where t6.NIN_CODE = t1.nin_code),'N') RECODING_EDITING,
select 'Y'||TEMPEDIT_BROKER from CLIENTS_TEMP_EDIT T6
UPDATE AGENT_CLIENTS set
Update AGENT_CLIENTS set AGENT_NIN=
update AGENT_CLIENTS set CLIENT_NIN = CLIENT_NIN-1000000000 where BROKER_CODE ='
Update AGENT_IMAGE set
UPDATE BROKERS_CONTACT SET
update BROKERS_STATUS set broker_status = '
update clients set CLIENT_STATUS = '
update clients set CLIENT_STATUS = '0', SUSPEND_RESUME_CODE = '
update clients set CLIENT_STATUS = '4', SUSPEND_RESUME_CODE = '4', SUSPEND_RESUME_USER = '
UPDATE CLIENTS SET(CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY, CLIENT_MAIN_TYPE, CLIENT_SUB_TYPE, DATE_OF_BIRTH, CLIENT_ADDRESS, COUNTRY, GOVERNORATE, UPDATE_DATE, UPDATE_BROKER_CODE, UPDATE_USER ,ID_ISSUE_DATE , ID_EXPIRE_DATE ,HUSBAND_NAME ,HUSBAND_NAME_comp , Job_Desc , FUND_TYPE ) =
UPDATE CLIENTS T1 SET(T1.CLIENT_GENDER, T1.ID_TYPE, T1.ID_NUMBER, T1.CLIENT_NATIONALITY, T1.CLIENT_MAIN_TYPE, T1.CLIENT_SUB_TYPE, T1.DATE_OF_BIRTH, T1.CLIENT_ADDRESS, T1.COUNTRY, T1.GOVERNORATE, T1.UPDATE_DATE, T1.UPDATE_BROKER_CODE, T1.UPDATE_USER, T1.TEMPEDIT_USER, T1.TEMPEDIT_DATE, T1.TEMPEDIT_BROKER ,T1.ID_ISSUE_DATE, T1.ID_EXPIRE_DATE, T1.JOB_DESC, T1.HUSBAND_NAME ,T1.HUSBAND_NAME_comp ,T1.FUND_TYPE) =
Update Clients_Contact set (LAST_BROKER,LAST_User,LANDLINE,MOBILE_PHONE,EMAIL)
Update Clients_Contact set (LAST_User,LANDLINE,MOBILE_PHONE,EMAIL , UPDATE_DATE)
Update clients_contact set nin_code =
update CLIENTS_contact set(LAST_BROKER,LAST_User,LANDLINE,MOBILE_PHONE,EMAIL , UPDATE_DATE)=
UPDATE CLIENTS_REACTIVE SET ACTION = 'A',ACTION_USER =
update CLIENTS_REACTIVE set ACTION ='R' , ACTION_DATE =sysdate , ACTION_user=
update CLIENTS_TEMP set confirm_Date = Sysdate , data_confirm = '1',confirm_temp_user = '
update clients_temp_edit set (CLIENT_GENDER, ID_TYPE, ID_NUMBER, CLIENT_NATIONALITY,
update CLIENTS_TEMP_EDIT set CLIENT_STATUS = '
update clients_Temp_Edit set CLIENT_STATUS = '0', SUSPEND_RESUME_CODE = '
UPDATE CLIENTSOWNER.brokers_screens SET STATUS =0 , ENABLED_Date = sysdate,Disable_Date =NULL where Broker_code='
UPDATE CLIENTSOWNER.brokers_screens SET STATUS =2 , Disable_Date = sysdate where Broker_code='
update CODING_BULLETINS set BULLETIN_MESSAGE = 'تم إغلاق النظام عن طريق إدارة التكويد فى هذا الوقت' where bulletin_code =
update groups set transtime = ltrim(rtrim('
Update REQUEST_SUSPEND_CLIENTS set
Update REQUEST_SUSPEND_CLIENTS set REQUEST_DATE =trunc(sysdate) ,Dead_DATE =
UPDATE SUSPEND_RESUME_IMAGE set Cancel = 1 , CANCEL_DATE = sysDate Where NIN_CODE ||'--' || To_Char(Scan_Date,'YYYY/MM/DD HH24:MI:SS')||'--'|| File_number in (
UPDATE USERS SET ip_address = '
UPDATE USERS SET USERNAME = upper('
