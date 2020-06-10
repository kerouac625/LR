
oracle 異常處理
    1.  安裝：
        1>  centos7版本，報錯后修改以下文件內容：$ORACLE_HOME/sysman/lib/ins_emagent.mk 在$(MK_EMAGENT_NMECTL)后添加 -lnnz11 注意- 前有空格   
            sed -i 's/$(MK_EMAGENT_NMECTL)/$(MK_EMAGENT_NMECTL) -lnnz11/' ins_emagent.mk
        2>  安裝數據庫報錯，File "/etc/oratab" is not accessible ；
            sh /home/oracle/oraInventory/orainstRoot.sh sh /home/oracle/product/11.2.4/dbhome_1/root.sh
        3>  安裝要求： swap:16G / 目錄 150~200G /data 分區
            --                 
    2.  誤刪除表數據
        select * from SFIS1.C_ASY_BOM_T as of timestamp to_timestamp('2019-04-08 18:40:00','yyyy-mm-dd hh24:mi:ss')
             
    3.  關聯表update
        update MACHAO_TEST2 a set a.COURSE=(select b.EMP_NAME from MACHAO_TEST b where a.EMP_NO=b.EMP_NO)

    4.  Oracle 锁表与解锁表
		1>  查询被锁的会话ID： select session_id from v$locked_object;
		2>	查詢哪張表被鎖：
			SELECT B.OWNER, B.OBJECT_NAME, A.SESSION_ID, A.LOCKED_MODE
			FROM V$LOCKED_OBJECT A, DBA_OBJECTS B
			WHERE B.OBJECT_ID = A.OBJECT_ID;
        3>  查询上面会话的详细信息：SELECT sid, serial#, PADDR,username, osuser,MACHINE,PORT,PROGRAM,SQL_ADDRESS,SQL_ID FROM v$session  where sid = session_id ;
			SELECT sid, serial#, PADDR,username, osuser,MACHINE,PORT,PROGRAM FROM v$session;
		4>	查詢被鎖的會話所執行的sql: SELECT a.sid, a.serial#, a.PADDR,a.username, a.osuser,a.MACHINE，a.PORT,a.PROGRAM,b.SQL_FULLTEXT FROM v$session a v$sqlarea b where a.SQL_ID=b.SQL_ID a.sid = session_id ;
        5>  将上面锁定的会话关闭： ALTER SYSTEM KILL SESSION 'sid,serial#';   【select  'ALTER SYSTEM KILL SESSION '''||sid||','||serial#||''';' from v$session where username='MWEB'】
        6>  查看已經殺掉但是還沒有釋放的進程，注：該情況需要在操作系統kill
			select session_id from v$locked_object;
			select a.spid,b.sid,b.serial#,b.username from v$process a,v$session b where a.addr=b.paddr and b.status='KILLED';
		7>	根據PID查詢消耗內存的sql 查詢select s.sid,s.serial# from v$session s,v$process p where s.paddr=p.addr and p.spid='9999';
		
		8>	使用alter system kill 之後沒有釋放的session ,在os 繼續殺
			select a.SID,a.SERIAL#,a.STATUS,a.SERVER,a.PROCESS,a.PROGRAM,a.LOGON_TIME,b.PID,b.SPID,b.USERNAME,b.SERIAL# SERIAL,b.PROGRAM pgm,b.TRACEFILE FROM V$SESSION a left join V$process b on a.creator_addr=b.ADDR where a.STATUS='KILLED'
			select 'kill -9 '||b.SPID from  v$session a,v$process b where  a.status='INACTIVE' and a.PADDR=b.ADDR and a.username='AQMS_AP';
			select b.spid,a.sid,a.serial#,a.machine from v$session a,v$process b where a.paddr =b.addr  and a.sid = '3'
			
    5.  修改安裝完 oracle 的 hostname 和 ip
        1>  修改host 文件 vi /etc/hosts
        2>  修改network 文件 (vi  /etc/sysconfig/network    NETWORKING=yes  HOSTNAME=XXX)
		2>  修改ip vi /etc/sysconfig/network-script/ifcfg-網卡 service network restart
    6.  REDO 文件損壞的處理以下幾種情況
        1>  損壞的是已歸檔且inactive,重建redo 即可,不會丟失數據
            alter database clear logfile group；
            alter database open;
        2>  損壞的是已歸檔且active或current 的redo 文件，在回復時，會丟失數據(已經commit 但是沒有寫到磁盤的數據會丟失)
			create pfile from spfile
			vi  pfile   添加 *._allow_resetlogs_corruption=TRUE
			create spfile from pfile
			startup mount
			recover database until cancel;
			cencel   
			alter database open resetlogs;
			select open_mode from v$database;			
    7.  share pool 爆滿的解決辦法:错误原因：共享内存太小，存在一定碎片，没有有效的利用保留区，造成无法分配合适的共享区。
		1>  查看当前环境
			SQL>show sga			　　
			Total System Global Area　566812832 bytes
			Fixed Size　　　　　　　　　　73888 bytes
			Variable Size　　　　　　　28811264 bytes
			Database Buffers　　　　　536870912 bytes
			Redo Buffers　　　　　　　　1056768 bytes
			
			SQL>show parameter shared_pool			
			NAME　　　　　　　　　　　　　　　　 TYPE　　VALUE
			------------------------------------ ------- -----
			shared_pool_reserved_size　　　　　　string　1048576
			shared_pool_size　　　　　　　　　　 string　20971520

			SQL> select sum(free_space) from v$shared_pool_reserved;			　
			SUM(FREE_SPACE)
			---------------
			　　1048576
			我们可以看到没有合理利用保留区
			
			SQL> SELECT SUM(RELOADS)/SUM(PINS) FROM V$LIBRARYCACHE;
			SUM(RELOADS)/SUM(PINS)
			----------------------
			　.008098188
			不算太严重
			
			SQL> SELECT round((B.Value/A.Value)*100,1) hardpaseperc
			FROM V$SYSSTAT A,V$SYSSTAT B
			WHERE A.Statistic# = 171 AND B.Statistic# = 172 AND ROWNUM = 1;　
			hardpaseperc
			------------------
			26.5　
		2>  查看保留区使用情况
			SQL>SELECT FREE_SPACE,FREE_COUNT,REQUEST_FAILURES,REQUEST_MISSES,LAST_FAILURE_SIZE FROM V$SHARED_POOL_RESERVED;
			FREE_SPACE FREE_COUNT REQUEST_FAILURES REQUEST_MISSES LAST_FAILURE_SIZE
			---------- ---------- ---------------- -------------- -----------------
			1048576　　　　　1　　　　　　　146　　　　　　　0　　　　　　　4132
			最近一次申请共享区失败时该对象需要的共享区大小4132　
			
			SQL>select name from v$db_object_cache where sharable_mem = 4132;
			name
			----------------
			dbms_lob
			-- dbms_lob正是exp时申请保留区的对象
		
		3>  查看导致换页的应用
			SQL> select * from x$ksmlru where ksmlrsiz>0;
			ADDR　　 INDX　　INST_ID KSMLRCOM　　　         KSMLRSIZ　KSMLRNUM       KSMLRHON             KSMLROHV KSMLRSES　
			50001A88  0　　　　　1    BAMIMA: Bam Buffer　    4100　　　　 64            DBMS_DDL              402745060 730DEB9C　　
			50001ACC  1　　　　　1    BAMIMA: Bam Buffer　    4108　　　　736            DBMS_SYS_SQL           1909768749 730D0838
			50001B10  2　　　　　1    BAMIMA: Bam Buffer　    4112　　　 1576            STANDARD              2679492315 730D7E20
			50001B54  3　　　　　1    BAMIMA: Bam Buffer    　4124　　　 1536            DBMS_LOB              853346312 730DA83C
			50001B98  4　　　　　1    BAMIMA: Bam Buffer　    4128　　　 3456            DBMS_UTILITY           4041615653 730C5FC8　
			50001BDC  5　　　　　1    BAMIMA: Bam Buffer　     4132　　　 3760           begin :1 := dbms_lob.getLeng...　2942875191 730CFFCC
			50001C20  6　　　　　1    state objects　　　         4184　　　 1088                                  0 00
			50001C64  7　　　　　1    library cache　　　         4192　　　　488                    EXU8VEW　                  2469165743 730C1C68
			50001CA8  8　　　　　1    state objects　　　         4196　　　　 16                                  0 730C0B90
			50001CEC  9　　　　　1    state objects　　　         4216　　　 3608                                   0 730D0838　
		
		4>  分析各共享池的使用情况
			SQL> select KSPPINM,KSPPSTVL from x$ksppi,x$ksppcv
			where x$ksppi.indx = x$ksppcv.indx and KSPPINM = '_shared_pool_reserved_min_alloc';　
			KSPPINM　　　　 KSPPSTVL
			-------------------------------　 --------
			_shared_pool_reserved_min_alloc　 4400　　--(门值)
			我们看到INDX=5,DBMS_LOB造成换页(就是做exp涉及到lob对象处理造成的换页情况),换出最近未使用的内存,但是换出内存并合并碎片后在共享区仍然没有合适区来存放数据,说明共享
			区小和碎片过多，然后根据_shared_pool_reserved_min_alloc的门值来申请保留区,而门值为4400,所以不符合申请保留区的条件,造成4031错误。我们前面看到保留区全部为空闲状态,所以我们可以
			减低门值，使更多申请共享内存比4400小的的对象能申请到保留区，而不造成4031错误。
		5>  解决办法：
			增大shared_pool （在不DOWN机的情况下不合适）
			打patch　 （在不DOWN机的情况下不合适）
			减小门值 （在不DOWN机的情况下不合适）
			
			因为LAST_FAILURE_SIZE<_shared_pool_reserved_min_alloc所以表明没有有效的使用保留区
			SQL> alter system set "_shared_pool_reserved_min_alloc" = 4000;
			alter system set "_shared_pool_reserved_min_alloc"=4000
			ERROR at line 1:
			ORA-02095: specified initialization parameter cannot be modified
			
			-- 9i的使用方法alter system set "_shared_pool_reserved_min_alloc"=4000 scope=spfile;
			使用alter system flush shared_pool; (不能根本性的解决问题)
			使用dbms_shared_pool.keep
		
		6>  由于数据库不能DOWN机，所以只能选择3)和4)
			运行dbmspool.sql
			SQL> @/home/oracle/products/8.1.7/rdbms/admin/dbmspool.sql
			找出需要keep到共享内存的对象
			SQL> select a.OWNER,a.name,a.sharable_mem,a.kept,a.EXECUTIONS ,b.address,b.hash_value
			from v$db_object_cache a,v$sqlarea b
			where a.kept = 'NO' and(( a.EXECUTIONS > 1000 and a.SHARABLE_MEM > 50000) or　a.EXECUTIONS > 10000) and SUBSTR(b.sql_text,1,50) = SUBSTR(a.name,1,50);
			OWNER　　NAME　　　　　　　　　　　　SHARABLE_MEM KEP EXECUTIONS ADDRESS　HASH_VALUE
			-------　----------------------—---　------------ --- ---------- -------- ----------
			SELECT COUNT(OBJECT_ID)　　 98292　　　　NO　 103207　　74814BF8 1893309624
			FROM ALL_OBJECTS
			WHERE OBJECT_NAME = :b1
			AND OWNER = :b2
			STANDARD　　　　　　　　　　286632　　　 NO　 13501
			DBMS_LOB　　　　　　　　　    　98292　 NO　 103750
			DBMS_LOB　　　　　　　     47536　　　　NO　 2886542
			DBMS_LOB　　　　　　　     11452　　　　NO　 2864757
			DBMS_PICKLER　　　　　　　　10684　　　　NO　 2681194
			DBMS_PICKLER　　　　　　　　5224　　　　 NO　 2663860
			SQL> execute dbms_shared_pool.keep('STANDARD');
			SQL> execute dbms_shared_pool.keep('74814BF8,1893309624','C');
			SQL> execute dbms_shared_pool.keep('DBMS_LOB');
			SQL> execute dbms_shared_pool.keep('DBMS_PICKLER');
			SQL> select OWNER, name, sharable_mem,kept,EXECUTIONS from v$db_object_cache where kept = 'YES' ORDER BY sharable_mem;
			SQL> alter system flush shared_pool;
			System altered.　　
			SQL> SELECT POOL,BYTES FROM V$SGASTAT WHERE NAME ='free memory';			　　
			POOL　　　　　　 BYTES
			----------- ----------
			shared pool　　7742756
			large pool　　　614400
			java pool　　　　32768
			[oracle@ali-solution oracle]$ sh /home/oracle/admin/dbexp.sh
			[oracle@ali-solution oracle]$ grep ORA- /tmp/exp.tmp
			未发现错误，导出数据成功　
			
			建议：
			由于以上解决的方法是在不能DOWN机的情况下，所以没能动态修改初始化参数，但问题的本质是共享区内存过小，需要增加shared pool，使用绑定变量，才能根本
			的解决问题，所以需要在适当的时候留出DOWN机时间，对内存进行合理的配置。
			
    8.  游標超過系統設定值，ORA-01000: 超出打开游标的最大数
		1>  step 1: 查看数据库当前的游标数配置slqplus
			show parameter open_cursors;
		2>  step 2:查看游标使用情况
			select o.sid, osuser, machine, count(*) num_curs from v$open_cursor o, v$session s where user_name = 'user' and o.sid=s.sid group by o.sid, osuser, machine order by  num_curs desc;
		3>  step 3:查看游标执行的sql情况
			select o.sid, q.sql_text from v$open_cursor o, v$sql q where q.hash_value=o.hash_value and o.sid = 123;
		4>  step 4:根据游标占用情况分析访问数据库的程序在资源释放上是否正常,如果程序释放资源没有问题，则加大游标数。
			alter system set open_cursors=2000 scope=both;
		5>	補充：createStatement和prepareStatement 放在java 代碼的循環外，且最終需要調用close() 的方法
    9.  編譯存儲過程，顯示一直在執行中，查找被什麼進程佔用；
		1>  判斷 procedure 是否被鎖定；
			SELECT * FROM V$DB_OBJECT_CACHE WHERE name='PROC_BG_MATERIAL_BAK' AND LOCKS!='0';
		2>  查看是什麼session 佔用 procedure;		
			select /*+ rule*/ *  from V$ACCESS WHERE object='PROC_BG_MATERIAL_BAK';
		3>	查看session 情況；
			select * from v$session where SID=XX；
oracle 啟動文件類型管理
    1.  oracle 啟動階段所用到的文件；
        startup nomount         -> 这个阶段会打开并读取配置文件，从配置文件中获取控制文件的位置信息
        alter database mount    -> 这个阶段会打开并读取控制文件，从控制文件中获取数据文件和联机重做日志文件的位置信息
        alter database open      -> 这个阶段会打开数据文件和联机重做日志文件
    
    2.  REDO 日誌管理
        1>  查看在線重做日誌
		    set linesize 300;
		    column MEMBER for a70;
		    column IS_RECOVERY_DEST_FILE for a25;
		    select * FROM v$logfile;
		    select * from v$log ;

        2>  變更redo 文件名稱或者是位置
			SQL> select member from v$logfile;
			SQL> shutdown immediate
			[oracle@ora1 ~]$ mv /u02/app/oracle/oradata/orcl/redo01.log  /u02/app/oracle/oradata/orcl/redo/redo01.log			
			SQL> startup mount
			SQL> alter database rename file '/u02/app/oracle/oradata/orcl/redo01.log' to '/u02/app/oracle/oradata/orcl/redo/redo01.log';
			SQL> alter database open
			SQL> select member from v$logfile;

        3>  添加redo 文件或者是刪除redo log或日誌組管理;
			<1> 增加日誌組
				alter database add logfile group 1('/data/oradata/CAILAMEI/onlinelog/online03.log') size 50M;
				alter database add logfile group 11('/data1/oradata/epodb/redo10.log') size 500M;
			<2> 刪除日誌組
				alter database drop logfile group 1;  -->刪除redo (注意 redo 至少要留兩個日誌組，redo 不可以直接刪除，要切歸檔到inactive )
			<3> 增加日誌組成員
				alter database add logfile member '/data/oradata/CAILAMEI/onlinelog/online04.log' to group 7; --如果日誌組當前使用，創建后的member 會invali''d;switch 會沒有問題
			<4> 刪除日誌組成員
				alter database drop logfile member '/data/oradata/CAILAMEI/onlinelog/online04.log';
			<5> 增加standbby 日誌組
				SQL> ALTER DATABASE ADD STANDBY LOGFILE GROUP 4 ('/u01/app/oracle/oradata/orcl/redo04.log') size 50M; 
			<6> 刪除 standby 日誌組
				ALTER DATABASE DROP STANDBY LOGFILE GROUP 4; 
			<7> 增加standby 日誌組成員
				alter database add STANDBY logfile member '/data/oradata/CAILAMEI/onlinelog/online10.log' to group 5;
			<8> 刪除standby 日誌組成員
				alter database  drop STANDBY logfile member '/data/oradata/CAILAMEI/onlinelog/online10.log';
				ALTER DATABASE 
			<9> 重命名日誌組成員
				RENAME FILE '/diska/logs/log1a.rdo', '/diska/logs/log2a.rdo'   TO '/diskc/logs/log1c.rdo', '/diskc/logs/log2c.rdo'

        
    3.  歸檔日誌管理
        1>  啟用歸檔
			SQL> startup mount 
			SQL> alter database archivelog
			SQL> alter system set log_archive_dest_1='location=/data/archivelogtes2t' scope=both; (mount 和open和read only 狀態下均可創建歸檔)
        2>  切歸檔
			SQL>alter system switch logfile; (只能在open狀態下切歸檔)

        3>  關閉歸檔
			流程：shutdown immediate >startup mount > alter database noarchivelog > alter database open > archive log list;
        4>  刪除歸檔：
			RMAN> delete expired archivelog all;
			RMAN> delete archivelog all completed before 'sysdate-1';
	
	4.	密碼文件管理
		1>	windows 環境
			C:/Documents and Settings/>orapwd file=D:/oracle/product/10.1.0/Db_1/database/PWDorcl.ORA password=admin entries=40 force=y; 
		2>	linux 環境
			orapwd file=$ORACLE_HOME/dbs/orapwCAILAMEI password=Foxconn123 entries=5 force=y;立即生效 password 是sys 的密碼			
oracle 工具類基本操作     
	1.  sqlplus工具	
		1>  sqlplus 基本命令
			show linesize : 查看当前设置的sqlplus输出的最大行宽
			set linesize : 设置sqlplus输出的最大行宽
			set pagesize：設置sqlplus 分頁，若不需要分頁，則 set pagesize 0;
			column : 修改显示字段的长度或名称
			column col_name format a15         将列col_name（字符型）显示最大宽度调整为15个字符
			column col_num format 999999      将列col_num（num型）显示最大宽度调整为6个字符
			column col_num heading col_num2   将col_num的列名显示为col_num2;
		2>	sqlplus 初始化參數文件及參數詳解
			$ORACLE_HOME/sqlplus/admin/glogin.sql
			set feedback on -- 顯示sql 語句查詢或者更新的行數
			set linesize 600
			set pagesize 600
			set name new_value gname  設定列的別名
			set sqlprompt "_user'@'_connect_identifier'> '" 
			

	2.  數據泵之導出/導入(expdp/impdp)
		1>  查看/創建目錄/授權
			select * from dba_directories;
			create directory exp_backup as '/data/expdata';  create directory DUMP_DIR as '/data/expbak';  
			grant read,write on directory  exp_backup to system; grant read,write on directory DUMP_DIR to system;

		2>  導出:
			在Windows平台下，需要对象双引号进行转义，使用转义符\;
			在linux 環境下在未使用parfile文件的情形下，所有的符号都需要进行转义，包括括号，双引号，单引号等】基本命令
			expdp system/sys123sys directory=DUMP  dumpfile=netapp_%U.dmp logfile=netapp_20190621.log compression=all full=Y  PARALLEL=4
			壓縮導出： 
			compression=all/DATA_ONLY/METADATA_ONLY默認/NONE/DEFAULT(METADATA_ONLY/NONE/10g)
			導出表：
			windows 環境
			tables=用戶名.表名,用戶名.表名  QUERY = '表名1:"where deptno =20"','表名2:"where deptno <=20 and deptno >=10"'
			linux 環境
			tables=\'sfis1.AI_AP_CM602_INI\',\'sfis1.AI_AP_CM602_SET\' 
			導出 schema：
			expdp system/sys123sys directory=DUMP_DIR dumpfile=sfism4.dmp logfile=sfism4.log schemas=sfism4,sfis1
			導出對象定義/數據： 
			CONTENT={ALL | DATA_ONLY | METADATA_ONLY}
			控制执行任务的最大线程数：
			PARALLEL=4 FILESIZE=2M 配合%U 從1開始計數
			估算備份佔用空間大小不導出數據：
			ESTIMATE_ONLY={Y | N默認為導出數據} 配合估算方式 ESTIMATE={BLOCKS(默認大) | STATISTICS} 導出時不加dumpfile 且配合使用NOLOGFILE參數   
			排除對象導出： 
			【linux 環境】 EXCLUDE=SCHEMA:\"\IN \(\'SFISM4\'\)\"\
			【windows 環境】EXCLUDE=TABLE:"IN ('EMP','DEPT')",SEQUENCE,VIEW,INDEX:"= 'INDX_NAME'",PROCEDURE:"LIKE 'PROC_U%'(_代表任意字符),\"TABLE:\"> 'E' \"(大于字符E的所有表对象)  
			eg.
			expdp system/sys123sys@tjepd1big directory=DUMP_DIR dumpfile=epd1big_full_20190808.dmp logfile=epd1big_full_0808.log full=y  EXCLUDE=SCHEMA:\"IN \(\'SFISM4\'\)\"
			
			分區表轉普通表
			
			partion-option=merge
			導出表空間：expdp system/Foxconn123  directory=EXPBAKUP dumpfile=tablespace.dmp logfile=tablespace.log  tablespaces=MACHAO

		3>  導入 
			REMAP_DATAFILE/SCHEMA/TABLESPACE 例如：remap_schema=scott:system
			TABLE_EXISTS_ACTION={SKIP | APPEND |TRUNCATE | FRPLACE } TABLE_EXISTS_ACTION=REPLACE
			impdp system/sys123sys  directory=DUMP  dumpfile=netapp_%U.dmp logfile=netapp_imp_20190622.log  full=Y  PARALLEL=4 resumabe=Y
	3.	RMAN 工具使用
		1>	RMAN中三个不完全恢复场景
			select systimestamp from dual;
			 run
				{
				set until time "to_date('2019-09-03:16:00:00','YYYY-MM-DD HH24:MI:SS')";
				restore database;
				recover database;
				}
				
	4.	oracle 日誌挖掘工具 dbms_logmnr
		1>	將歸檔日誌添加到LOGMNR
			exec dbms_logmnr.add_logfile(logfilename=>'/data/database/tjepd1db/arch/1_466410_785700410.dbf',options=>dbms_logmnr.new);
			exec dbms_logmnr.add_logfile(logfilename=>'/data/database/tjepd1db/arch/1_466411_785700410.dbf',options=>dbms_logmnr.addfile);
		2>	開始分析
			exec dbms_logmnr.start_logmnr(options=>dbms_logmnr.dict_from_online_catalog);
 
		3>	查看LOGMNR分析後的數據。
			select timestamp,sql_redo from v$logmnr_contents;
 
		4>	保存到table logmnr_contents
			create table logmnr_contents as select * from v$logmnr_contents;
 
		5>	查看logmnr_contents內容
			select OPERATION,DATA_OBJ#,count(OPERATION) from sys.logmnr_contents group by OPERATION,DATA_OBJ#
 
		6>	结束LOGMNR操作, drop table logmnr_contents
			exec dbms_logmnr.end_logmnr;
			drop table logmnr_contents PURGE;
ORACLE 相關設置            
	1.  .bash_profile  設置環境變量設置
		PATH=$PATH:$HOME/bin  它的作用是在原来的PATH变量加上家目录下的bin目录的路径，效果就是家目录下的bin目录的命令可以直接打出来执行
		export PATH
		umask 022 權限 755  ( 027-->750)
		unset USERNAME  用于删除已定义的shell变量
		export ORACLE_BASE=/home/oracle
		export ORACLE_SID=tjepd6db
		export ORACLE_HOME=/home/oracle/product/11.2.3/db_1
		export PATH=$ORACLE_HOME/bin:$PATH
		export NLS_LANG=AMERICAN_AMERICA.AL32UTF8
		export PS1=*\${ORACLE_SID}*$PS1 
		stty erase  ^H ctrl+backup 鍵,設置該環境變量只需要backup刪除即可；

	2.  創建快速回復區 (需要切換至mount 狀態下)設置
		1>  設置快速恢復區大小：alter system set db_recovery_file_dest_size = 2G scope=both;
		2>  設置路徑：alter system set db_recovery_file_dest='/u01/app/FAR' scope=both

	3.  oracle 管控ip 訪問之黑白名单 相关（在 文件 sqlnet.ora下設置）
		1>  TCP.VALIDNODE_CHECKING = YES  
			開啟黑白名單訪問按鈕，并使用这个参数来启用下边的两个参数。
		2>  TCP.EXCLUDED_NODES = (list of IP addresses)
			指定不允许访问oracle的节点，可以使用主机名或者IP地址
		3>  TCP.INVITED_NODES = (list of IP addresses)
			指定允许访问db的客户端，他的优先级比TCP.EXCLUDED_NODES高。
			注意：excluded_nodes与invited_nodes为互斥方式，不可以同时使用0
		4>	在設置完成黑白名單后，lsnrctl reload 不會影響現有的鏈接

	4.  登录方式限定 
		在 文件 sqlnet.ora下設置
		SQLNET.AUTHENTICATION_SERVICES= (NTS/NONE/ALL) 
		NTS: 允許ora_dba組中的用戶使用local windows 驗證
		NONE:不允許windows，但允許密碼驗證
		ALL:所有的认证方式都支持

	5.  其它限制
		时间限制
		连接超时时间，即连接300秒没有活动自动断开连接
		sqlnet.expire_time = 300

	6.  版本限制
		可以对客户端的版本进行限制;
		SQLNET_ALLOWED_LOGON_VERSION=8
		SQLNET.ALLOWED_LOGON_VERSION_SERVER=8
		SQLNET.ALLOWED_LOGON_VERSION_CLIENT=8
	7.  內存設置 
		1>  修改SGA 和PGA
			alter system set sga_max_size=83G scope=spfile;
			alter system set sga_target=83G scope=spfile;
			alter system set pga_aggregate_target=1G scope=spfile;

		2>  修改 memory
			關機Remove the MEMORY_MAX_TARGET=0 and MEMORY_TARGET=0 lines.
			開機alter system reset memory_target;
			alter system reset memory_max_target;
		3>	OS kernel 參數設置
			kernel.shmmax ：是核心参数中最重要的参数之一，用于定义单个共享内存段的最大值。
			kernel.shmall ：该参数控制可以使用的共享内存的总页数。 Linux 共享内存页大小为 4KB, 共享内存段的大小都是共享内存页大小的整数倍。
	8.  並發session 和processors 的設置（修改processes和sessions值必须重启Oracle服务器才能生效）sessions=(1.1*process+5)
		1>  SQL>show parameter processes;
			SQL>show parameter sessions ;
			SQL>alter system set processes=300 scope=spfile;
			SQL>alter system set sessions=335 scope=spfile ;
			SQL>commit ;
		2>	查看查询数据库当前进程的连接数
			select count(*) from v$process;
		3>	查看数据库当前会话的连接数：
			select count(*) from v$session;
		4>	查看数据库的并发连接数：
			select count(*) from v$session where status='ACTIVE';			
	9.	監聽設置
		lsnrctl set log_status off
		mv listener.log listener.log.10
		lsnrctl set log_status on
	10.	oracle 字符集編碼設置
		1>	UTF-32：統一使用4個字節表示一個字符，存在空間利用率問題；
		2>	UTF-16：相對常用的60000 多個字符使2個字節，其餘使用4個字節；
		3>	UTF-8 ：兼容ASCII 拉丁文，希臘文等使用2個字節，包括漢字在內的其他常用字符使用3個字節，剩下的極少的字符使用4個字節；
		4>	oracle 數據庫服務器字符集，
oracle 查看修改DB相關命令
	1.  用戶相關
		1>  創建用戶 create user CAILAMEI  identified by  default tablespace test_data  temporary tablespace temp;
		2>  更改用戶名密碼：alter user system identified by password;
		3>  解鎖用戶 alter user user_name account unlock;
		4>  查看用戶 select USERNAME,ACCOUNT_STATUS from dba_users;
		5>  設置所有用戶密碼不過期 
			SELECT USERNAME ,PROFILE FROM dba_users;
			SELECT * FROM dba_profiles s WHERE s.profile='DEFAULT' AND resource_name='PASSWORD_LIFE_TIME';
			ALTER PROFILE DEFAULT LIMIT PASSWORD_LIFE_TIME UNLIMITED;

		6>  設置單個用戶密碼不過期
			創建 profile                     
			CREATE PROFILE "VPXADMIN_UNLIMIT" LIMIT
			SESSIONS_PER_USER UNLIMITED
			CPU_PER_SESSION UNLIMITED
			CPU_PER_CALL UNLIMITED
			CONNECT_TIME UNLIMITED
			IDLE_TIME UNLIMITED
			LOGICAL_READS_PER_SESSION UNLIMITED
			LOGICAL_READS_PER_CALL UNLIMITED
			COMPOSITE_LIMIT UNLIMITED
			PRIVATE_SGA UNLIMITED
			FAILED_LOGIN_ATTEMPTS 10
			PASSWORD_LIFE_TIME 180
			PASSWORD_REUSE_TIME UNLIMITED
			PASSWORD_REUSE_MAX UNLIMITED
			PASSWORD_LOCK_TIME 1
			PASSWORD_GRACE_TIME 7
			PASSWORD_VERIFY_FUNCTION NULL;
			設置新的密碼profile 密碼不過期
			ALTER profile VPXADMIN_UNLIMIT limit PASSWORD_LIFE_TIME UNLIMITED;
			更換用戶profile 文件為新建的文件
			alter user CAILAMEI  profile VPXADMIN_UNLIMIT;
		7>  解鎖用戶		
			select USERNAME,STATUS from dba_users;
			alter user user_name account unlock
		8>  設置密碼不區分大小寫
			show parameter sec_case
			alter system set sec_case_sensitive_logon=false;
			在更新完密碼大小寫后，需要alter 被lock 的帳密；

	2.  授予權限方面
		1>	查詢oracle 的權限列表
			select * from session_privs;
			select * from dba_sys_privs; 查看用户或者角色系统权限
			
			
		2>  獲取schema 下所有的表或者其他對象，得出授予對象權限的腳本，適用於 給所有對象賦權限
			select 'Grant select  on MES1.'||table_name||' to  TJW_READ ;' from all_tables where owner = upper('MES1');  

		3>  授予當前用戶查詢其他用戶表的權限
			Grant select on MES1.C_SAPWORKORDER_TEMP to TJW_READ;

		4>  撤銷當前用戶查詢其他用戶表的權限
			revoke select on MES1.HR_GTWREPORTBYMONTH from TJW_READ;			
		5>  查詢用戶賦予對象的權限
			set pagesize 600
			select 'grant ' ||PRIVILEGE||' on ' ||OWNER||'.'||TABLE_NAME||' to '||GRANTEE||';'  from dba_tab_privs where grantor='SFIS1';
	3.  oracle 性能相關
		1>  查看運行慢的sql 
			select * from 
			(select sa.SQL_TEXT,sa.EXECUTIONS "执行次数",round(sa.ELAPSED_TIME / 1000000, 2) "总执行时间", 
			round(sa.ELAPSED_TIME / 1000000 / sa.EXECUTIONS, 2) "平均执行时间",sa.COMMAND_TYPE,
			sa.PARSING_USER_ID "用户ID",u.username "用户名",sa.HASH_VALUE 
			from v$sqlarea sa
			left join all_users u  on sa.PARSING_USER_ID = u.user_id
			where sa.EXECUTIONS > 0  order by (sa.ELAPSED_TIME / sa.EXECUTIONS) desc)
			where rownum <= 50;

		2>  查看運行最多的sql 
			select * from 
			(select s.SQL_TEXT,s.EXECUTIONS "执行次数",s.PARSING_USER_ID "用户名",rank() over(order by EXECUTIONS desc) EXEC_RANK
			from v$sql s
			left join all_users u on u.USER_ID = s.PARSING_USER_ID) t
			where exec_rank <= 100;
		
		3>	打印awr 報告
			@?/rdbms/admin/awrrpt.sql

	4.  查看oracle schema 對象相關sql  語句
		1>  DB_LINK (公有和私有)
			<1> 查看DB 所有的DBLINk
				set linesize 300;
				col HOST for a20;
				col USERNAME for a20; 
				col DB_LINK for a15;
				select * from ALL_DB_LINKS;

			<2> 創建全局 DBLINK
				CREATE PUBLIC DATABASE LINK EPD1BIG CONNECT TO sfis1 IDENTIFIED BY sfis1 USING '10.67.51.14:1560/tjepd1big';
				CREATE PUBLIC DATABASE LINK MYSQL5TEST CONNECT TO "easyweb" IDENTIFIED BY "webeasy" USING 'MYSQL5TEST';
				select count(*) from test_cailamei@MYSQL5TEST;
			<3> 刪除DBLINK
				DROP DATABASE LINK [name]; /  DROP PUBLIC DATABASE LINK [name];
			<4> 通過DBLINK 查詢數據
				select * from user3.table@testLink;

		2>  查詢創建對象的sql 腳本
			set long 999999;
			SET LINESIZE 1000 
			SET PAGESIZE 1000
			select dbms_metadata.get_ddl('TABLE','TABLE_NAME','TABLE_OWNER') from dual;  --創建表
			select dbms_metadata.get_ddl('INDEX','INDEX_NAME','INDEX_OWNER') from dual; --創建索引
			select dbms_metadata.get_ddl('VIEW','VIEW_NAME','VIEW_OWNER') from dual; --創建視圖
			select dbms_metadata.get_ddl('PROCEDURE','PROCEDURE_NAME','PROCEDURE_OWNER') fromdual;  --創建存儲過程
			select dbms_metadata.get_ddl('FUNCTION','FUNCTION_NAME','FUNCTION_OWNER') from dual;  --創建函數
			SELECT DBMS_METADATA.GET_DDL('CONSTRAINT','CONSTRAINTNAME','USERNAME') FROM DUAL; --查看创建主键的SQL
			SELECT DBMS_METADATA.GET_DDL('REF_CONSTRAINT','REF_CONSTRAINTNAME','USERNAME') FROM DUAL; --查看创建外键的SQL
			SELECT DBMS_METADATA.GET_DDL('USER','USERNAME') FROM DUAL; --查看用户的SQL
			SELECT DBMS_METADATA.GET_DDL('ROLE','ROLENAME') FROM DUAL;--查看角色的SQL
			SELECT DBMS_METADATA.GET_DDL('TABLESPACE','TABLESPACENAME') FROM DUAL; --查看表空间的SQL
			select dbms_metadata.get_ddl('MATERIALIZED VIEW','MVNAME') FROM DUAL; --获取物化视图SQL
			SELECT dbms_metadata.get_ddl('DB_LINK','DBLINKNAME','USERNAME') stmt FROM dual; --获取远程连接定义SQL--
			select DBMS_METADATA.GET_DDL('TRIGGER','TRIGGERNAME','USERNAME) FROM DUAL;--获取用户下的触发器SQL
			select DBMS_METADATA.GET_DDL('SEQUENCE','SEQUENCENAME') from DUAL; -获取用户下的序列
			select DBMS_METADATA.GET_DDL('PACKAGE','PACKAGENAME','USERNAME') from dual; --获取包的定义
			SELECT DBMS_LOB.SUBSTR@dblinkname(DBMS_METADATA.GET_DDL@dblinkname('TABLE', 'TABLENAME', 'USERNAME')) FROM DUAL@dblinkname  --获取远程数据库对象的定义					
		
		3>  查看并編譯 DB 無效的對象
			<1>	查看無效的對象
				SELECT owner,object_type,object_name,STATUS FROM dba_objects WHERE STATUS='INVALID' ORDER BY owner,object_type,object_name;
			<2> 手動編譯單個無效的對象
				SQL>ALTER PROCEDURE my_procedure COMPILE;
				SQL>ALTER FUNCTION my_function COMPILE;
				SQL>ALTER TRIGGER my_trigger COMPILE;
				SQL>ALTER VIEW my_view COMPILE;
			<3> 編譯schema 下所有的對象
				EXEC DBMS_UTILITY.compile_schema(schema =>'CAILAMEI');--使用这个包将会编译指定schema下的所有procedures, functions, packages, and triggers.
			<4> 執行組裝sql 批量編譯無效的對象
				SELECT 'alter '||object_type||' '||owner||'.'||object_name||' compile;' 
				FROM all_objects 
				WHERE status = 'INVALID'  AND object_type in ('FUNCTION','JAVA SOURCE','JAVA CLASS','PROCEDURE','PACKAGE','VIEW','TRIGGER'); 
				@$ORACLE_HOME/rdbms/admin/utlrp.sql sys 用戶下執行腳本，批量編譯無效的對象
		
		4>  查看并執行job
			<1> 查看job
				select * from all_jobs
				SELECT * FROM user_jobs
				select * FROM dba_jobs where LOG_USER='SFIS1'
				select * FROM dba_jobs_running
				select JOB, LOG_USER, broken  from dba_jobs;
				set linesize 300;
				查看job詳細信息
				col LOG_USER for a10;
				col priv_user for a10;
				col broken for a10;
				col what for a30;
				col job for a10;
				select job, what, log_user, priv_user,broken from dba_jobs where job=208;
			<2> 手動執行job
				exec DBMS_IJOB.broken(208,true);exec DBMS_JOB.BROKEN(486,TRUE)待驗證；
			<3> 創建job
				DECLARE jobno numeric; 
				BEGIN dbms_job.submit(jobno, 'SFISM4.COPY_FROM_STANDBY_EPD1;', sysdate+(9*60+15)/(24*60), 'sysdate+(10/1440)'); 
				COMMIT;
				END;
				
				DECLARE jobno numeric; 
				BEGIN dbms_job.submit(jobno,'SFIS1.BP_TESTTIME;',to_date('2020-01-07 16:00:00','yyyy-mm-dd hh24:mi:ss'),'NEXT_DAY(TRUNC(SYSDATE),1)+1/24');
				COMMIT;
				END;

			<4> 查看正在執行的job 的sid,spid;
				select b.SID,b.SERIAL#,c.SPID
				from dba_jobs_running a,v$session b,v$process c
				where a.sid = b.sid and b.PADDR = c.ADDR
			<5> 殺掉正在執行的job；
				ALTER SYSTEM KILL SESSION '1721,747';
				
			<6> job執行的時間設定
				描述                                 INTERVAL参数值
				每天午夜12点                      'TRUNC(SYSDATE + 1)'
				每天早上8点30分                  'TRUNC(SYSDATE + 1) + （8*60+30）/(24*60)'
				每星期二中午12点                  'NEXT_DAY(TRUNC(SYSDATE ), 'TUESDAY' ) + 12/24'
				每星期六和日早上6点10分    	   'TRUNC(LEAST(NEXT_DAY(SYSDATE, 'SATURDAY'), NEXT_DAY(SYSDATE, 'SUNDAY'))) + (6×60+10)/(24×60)' sunday 有時候需要寫成數字為1
				每个月第一天的午夜12点          'TRUNC(LAST_DAY(SYSDATE ) + 1)'
				每个季度最后一天的晚上11点     'TRUNC(ADD_MONTHS(SYSDATE + 2/24, 3 ), 'Q' ) -1/24'
		5>  查看某一個對象
			select  * from all_source where text like '%UPDATE SFISM4.R_WIP_TRACKING_T%';
		6>	創建同義詞
			CREATE [OR REPLACE] [PUBLIC] SYNONYM [当前用户.]synonym_nameFOR [其他用户.]object_name;
			
		
	5.	表分區
		1>  select * FROM dba_tables where owner='SFISM4' and partitioned='YES'		
	6.  表空間相關
		1>  查看表空間狀態
			select tablespace_name,status from dba_tablespaces;
		2>	表空間收縮 
			select  a.file_id,a.file_name,a.filesize, b.freesize, 
			(a.filesize-b.freesize) usedsize,  c.hwmsize,  
			c.hwmsize - (a.filesize-b.freesize) unsedsize_belowhwm,  
			a.filesize - c.hwmsize canshrinksize  
			from  
			(select file_id,file_name,round(bytes/1024/1024) filesize from dba_data_files ) a, 
			( select file_id,round(sum(dfs.bytes)/1024/1024) freesize from dba_free_space dfs group by file_id ) b, 
			( select file_id,round(max(block_id)*8/1024) HWMsize from dba_extents group by file_id) c 
			where a.file_id = b.file_id  and a.file_id = c.file_id 
			order by unsedsize_belowhwm desc;
			
	7.  數據文件相關
		1>  修改數據文件名稱或位置		
			<1> DB OPEN 狀態下offline 數據文件并重命名數據文件名稱或修改位置
				set linesize 300
				set pagesize 500
				col NAME for a80
				select NAME, status,ENABLED FROM v$datafile;
				alter database datafile '/data/oradata/CAILAMEI/datafile/CAILAMEI01.dbf' offline;
				alter database rename file '/data/oradata/CAILAMEI/datafile/CAILAMEI01.dbf' to '/data/oradata/CAILAMEI/datafile/test/CAILAMEI01test.dbf';
				alter database recover datafile 6;
				alter database datafile '/data/oradata/CAILAMEI/datafile/test/CAILAMEI01test.dbf' online;
				alter tablespace CAILAMEI offline;
			
			<2> DB OPEN 狀態下offline 表空間來修改數據文件名稱或位置的方法
				alter database rename file '/data/oradata/CAILAMEI/datafile/test/CAILAMEI01test.dbf' to '/data/oradata/CAILAMEI/datafile/CAILAMEI01.dbf';
				alter tablespace CAILAMEI online;
			
			<3> DB 停機修改數據文件名稱或位置:
				*  关闭数据库；                   
				*  复制数据文件到新的位置；       
				*  启动数据库到mount状态；        
				*  通过SQL修改数据文件位置；alter database rename file '/opt/oracle/oradata/ZERONE01.DBF' to '/home/oracle/oradata/zerone/ZERONE01.DBF';   
				*  打开数据库；
				*  檢查數據文件：select name from v$datafile;
		
		2>  查看某個表空間下的所有數據文件				
			select file_name,tablespace_name from dba_data_files where tablespace_name='ZERONE';
		3>  查看永久表空间的数据文件对应的表空间
			select TABLESPACE_NAME from dba_data_files where FILE_NAME='数据文件全路径';
		4>  查看临时表空间的数据文件对应的临时表空间
			select TABLESPACE_NAME from dba_temp_files where FILE_NAME='数据文件全路径';
	8.	oracle 日期函數
		select to_char(sysdate, 'yyyy') 年,
		to_char(sysdate, 'mm') 月,
		to_char(sysdate, 'DD') 日,
		to_char(sysdate, 'HH24') 时,
		to_char(sysdate, 'MI') 分,
		to_char(sysdate, 'SS') 秒,
		to_char(sysdate, 'DAY') 天,
		to_char(sysdate, 'Q') 第几季度,
		to_char(sysdate, 'W') 当月第几周,
		to_char(sysdate, 'WW') 当年第几周,
		to_char(sysdate, 'D') 当周第几天,
		to_char(sysdate, 'DDD') 当年第几天    
		from dual;
		*** alter   session set NLS_DATE_LANGUAGE = American;***
	9.	oracle 參數設置(動態&靜態)
		select distinct ISSYS_MODIFIABLE from v$parameter；
		IMMEDIATE --動態參數
		FALSE --靜態參數
		DEFERRED --動態參數
		--查詢參數的靜態或動態屬性
		select name,ISSYS_MODIFIABLE from v$parameter  where name='sec_case_sensitive_logon';
	10.	Oracle 查看存储过程占用，及编译时卡住问题；
		1>  查看存储过程是否有锁住 --LOCKS!='0' 即表示有锁，正在执行 --name 这里也可以用like来模糊拆线呢
			SELECT * FROM V$DB_OBJECT_CACHE WHERE name='存储过程名称' AND LOCKS!='0';
		2>  找到锁住过程的SID ---object这里一样可以用 like  模糊
			select  SID from V$ACCESS WHERE object='存储过程名称';
		3>  查看锁住存储过程对象的设备信息，包括是那台机器锁定的,什么时间锁住的，等等都可以通过以下语句查到
			SELECT *  FROM V$SESSION WHERE SID='SID';
		4>  强制kill进程,先找到要杀死进程的sid 和 serial# ,然后进行kill (注意， 这里的alter命令，可以加immediate 也可以不加immediate,加immediate ，表示标记执行，类似异步吧,不加immediate：表示直接立即执行，这个时候有可能出现plsql程序假死的情况。)
			SELECT SID,SERIAL#,PADDR FROM V$SESSION WHERE SID='sid';
			alter system kill session 'SID，Serial#' immediate 
	11.	resize 數據文件
		select a.file#,a.name,a.bytes / 1024 / 1024 CurrentMB,
		ceil(HWM * a.block_size) / 1024 / 1024 ResizeTo,
		(a.bytes - HWM * a.block_size) / 1024 / 1024 ReleaseMB,'alter database datafile ''' || a.name || ''' resize ' ||ceil(HWM * a.block_size) / 1024 / 1024 || 'M;' ResizeCmd
		from v$datafile a,
		(SELECT file_id, MAX(block_id + blocks - 1) HWM FROM DBA_EXTENTS
		GROUP BY file_id) b
		where a.file# = b.file_id(+)
		And (a.bytes - HWM * a.block_size) >0
		order by ReleaseMB desc;
	12.	刪除數據庫；（數據文件& 控制文件 & 日誌文件）
		SQL> shutdown immediate
			 Database closed.
			 Database dismounted.
			 ORACLE instance shut down.
		SQL> startup nomount;
			 ORACLE instance started.
			 Total System Global Area 1820540928 bytes
			 Fixed Size		    2229304 bytes
			 Variable Size		  855641032 bytes
			 Database Buffers	  956301312 bytes
			 Redo Buffers		    6369280 bytes
		SQL> alter database mount exclusive;
			 Database altered.
		SQL> alter system enable restricted session;
			 System altered.
		SQL> drop database;
			 Database dropped.
			 
	13.	查看oracle 安裝的組件：
		SQL> set pagesize 1000
		SQL> col comp_name format a36
		SQL> col version format a12
		SQL> col status format a8
		SQL> col owner format a12
		SQL> col object_name format a35
		SQL> col name format a25
		select comp_name, version, status from dba_registry;
		
		COMP_NAME			               VERSION	   STATUS
		OWB				              11.2.0.3.0   VALID
		Oracle Application Express	          3.2.1.00.12  VALID
		Oracle Enterprise Manager	          11.2.0.3.0   VALID
		OLAP Catalog			              11.2.0.3.0   VALID
		Spatial 			                  11.2.0.3.0   VALID
		Oracle Multimedia		              11.2.0.3.0   VALID
		Oracle XML Database		          11.2.0.3.0   VALID
		Oracle Text			              11.2.0.3.0   VALID
		Oracle Expression Filter	              11.2.0.3.0   VALID
		Oracle Rules Manager		          11.2.0.3.0   VALID
		Oracle Workspace Manager	          11.2.0.3.0   VALID
		Oracle Database Catalog Views	      11.2.0.3.0   VALID
		Oracle Database Packages and Types    11.2.0.3.0   VALID
		JServer JAVA Virtual Machine	      11.2.0.3.0   VALID
		Oracle XDK			              11.2.0.3.0   VALID
		Oracle Database Java Packages	      11.2.0.3.0   VALID
		OLAP Analytic Workspace 	          11.2.0.3.0   VALID
		Oracle OLAP API 		              11.2.0.3.0   VALID


		
oracle DG 搭建相關命令
	1.  查看數據庫角色以及狀態
		select database_role,switchover_status from v$database;          
	2.  備庫查看歸檔日誌序號。
	3.  已經應用成功的日誌
		set pagesize 100
		col name for a58
		col applied for a10
		select sequence#,name,applied from v$archived_log order by sequence#;

	4.  正在應用中的日誌
		col name for a58
		col applied for a10
		select sequence#,name,applied from v$archived_log where applied='IN-MEMORY' ;

	5.  沒有應用的日誌
		col name for a58;
		col applied for a10;
		select sequence#,name,applied from v$archived_log where applied='NO' ;
	6.  查看自動同步文件的方式
		show parameter standby
		ALTER SYSTEM SET standby_file_management='AUTO'  SCOPE=BOTH;
	7.	修改pfile 參數值(靜態參數+動態參數)
		alter system set db_unique_name='cailamei_pr'  scope=spfile;
		alter system set fal_client='agile9_PRI' scope=both;
		alter system set fal_server='agile9_STY' scope=both;
		alter system set log_archive_config='dg_config=(agile9,agile9_st)' scope=both;
		alter system set log_archive_dest_2='SERVICE=agile9_STY LGWR ASYNC VALID_FOR=(ONLINE_LOGFILES,PRIMARY_ROLE) DB_UNIQUE_NAME=agile9_st' scope=both;
		alter system set log_archive_dest_state_1=enable;
		alter system set log_archive_dest_state_2=enable;
		alter system set standby_file_management='AUTO';
		alter database force logging;
		alter system set db_file_name_convert='/data/oradata/agile9/','/data/oradata/agile9/' scope=spfile;
		alter system set log_file_name_convert='/data/oradata/agile9/','/data/oradata/agile9/' scope=spfile;
	8.	備援server 應用日誌相關
		開啟應用重做：alter database recover managed standby database disconnect from session;(無standby redo log)
		實時應用重做：alter database recover managed standby database using current logfile disconnect;(有standby redo log)
		關閉應用重做：alter database recover managed standby database cancel;
oracle DG switchover 和failover
	1.  Switchover 角色切換
		備援：
		--col name for a58;
		--col applied for a10;
		--select sequence#,name,applied from v$archived_log where applied='NO' ;
		主庫：
		--alter system switch logfile
		--archive log list
		--select database_role,switchover_status from v$database;
		--alter database commit to switchover to physical standby with session shutdown;
		--select database_role,switchover_status from v$database;
		--shutdown immediate
		備援：
		--alter database recover managed standby database cancel;
		--select database_role,switchover_status from v$database;
		--alter database commit to switchover to primary with session shutdown;
		--alter database open 
		--select database_role,switchover_status from v$database;
		--alter system switch logfile
		主庫：
		--startup mount
		--alter database open read only;
		--alter database recover managed standby database disconnect from session;(無standby redo log)
		--alter database recover managed standby database using current logfile disconnect;(有standby redo log)
		--select sequence#,name,applied from v$archived_log where applied='IN-MEMORY';
		
	2.  Failover 故障切換
		備援：
		alter database recover managed standby database cancel;
		alter database recover managed standby database finish force;
		select database_role from v$database;
		alter database commit to switchover to primary;
oracle snapshot standby 搭建
	1.	查看閃回是否開啟
		select open_mode,log_mode,flashback_on from v$database;
	2.	查看閃回文件大小及存放路徑
		show parameter recovery
		NAME				          TYPE	      VALUE
		------------------------- -----------   ----------- 
		db_recovery_file_dest		     string
		db_recovery_file_dest_size	     big integer     0
		recovery_parallelism		     integer	    0
	3.	設置	db_recovery_file_dest及db_recovery_file_dest_size
		alter system set db_recovery_file_dest='/data/flashback' scope=spfile;
		alter system set db_recovery_file_dest_size=2G scope=spfile;
	4.	重啟DB至mount 狀態
		shutdown immediate
		startup mount
	5.	開啟閃回
		alter database flashback on;   ---在mount 情況下開啟閃回，且必須在歸檔模式下
	6.	開啟DB 并查看閃回是否開啟
		alter database open;
		select open_mode ,log_mode,flashback_on from v$database;
		OPEN_MODE	  LOG_MODE    FLASHBACK_ON
		------------ ------------  ---------------
		READ ONLY	  ARCHIVELOG   YES
	7.	切換DB 為 napshot standby 并查看 切換后的DB 狀態
		alter database convert to snapshot standby;
		select open_mode ,log_mode,flashback_on,database_role from v$database;
		
		OPEN_MODE	   LOG_MODE	FLASHBACK_ON     database_role
		------------ ------------  ---------------   ----------------
		MOUNTED 	   ARCHIVELOG   YES             SNAPSHOT STANDBY
	8.	查看閃回文件的大小和時間點
		select name,storage_size from v$restore_point;
		NAME                                                     STORAGE_SIZE
		----------------------------------------------------    ---------------
		SNAPSHOT_STANDBY_REQUIRED_09/29/2019 16:41:33             52428800

	9.	開啟DB
		alter database open;
oracle snapshot standby 切換為physical standby 
	shutdown immediate;
	startup mount
	alter database convert to physical standby;
	shutdown immediate
	startup
	select open_mode,database_role from v$database;

	OPEN_MODE	   DATABASE_ROLE
	-------------- ----------------
	READ ONLY	   PHYSICAL STANDBY

	alter database recover managed standby database using current logfile disconnect;

	
oracle sql 優化

SQL_TRACE和10046 事件详解
sql_trace和10046事件都是我们在优化sql上面应用的非常多的工具，我们可以使用这两个工具知道当前正在执行的sql究竟在做什么
一，SQL_TRACE： SQL_TRACE命令会将执行的整个过程输出到一个trace文件，我通过阅读这个trace文件来了解这个sql在执行过程中Oracle究竟做了哪些事情
1. 确定trace文件的路径及trace 文件名稱
	1>	SQL> show parameter user_dump_dest，查詢出trace 的路徑
		NAME             TYPE      VALUE
		-------------   -------- ------------------------------------------
		user_dump_dest    string   /home/oracle/diag/rdbms/cailamei/CAILAMEI/trace
	
	2>	查詢當前session 的tracefile;
		SQL> select tracefile from v$process where addr=(select paddr from v$session where sid=(select distinct sid from v$mystat));	
		TRACE_FILE
		-------------------------------------------------------------
		/home/oracle/diag/rdbms/cailamei/CAILAMEI/trace/CAILAMEI_ora_1667.trc

	3>	可以手工更改产生trace文件的名称
		SQL> alter session set tracefile_identifier='mytrace'
		結果是 /home/oracle/diag/rdbms/cailamei/CAILAMEI/trace/CAILAMEI_ora_1667_mytrace.trc



2）启用sql_trace

启用sql_trace可以通过从instance级别和session级别，我们知道每个session都会有一个trace文件，因此在实例级别启用如果session越多将暂用的系统资源越多，除非有特殊的需求。

下面的语句可以启用实例级别的trace

SQL> alter system set sql_trace=true
SQL> show parameter sql_trace
NAME     TYPE             VALUE
--------- ----------- -------------
sql_trace   boolean           TURE


从session级别启动有两种方式，第一跟踪当前的session，第二跟踪其它的session，我们只需要确定备跟踪会话的sid和serial#的值就可以了



当前session：

SQL> alter session set sql_trace=ture;

其它session：

SQL> select sid,serial# from v$session where sid=138;
	SID          SERIAL#
	---------- ----------
	138            2397


然后执行下面的包来跟踪这个session

開始：SQL> execute dmbs_system.set_sql_trace_in_session(138,2397,true)

停止：SQL> execute dbms_system.set_sql_trace_in_session(138,2397,false)


二，TKPROF工具

默认生成的trace文件的可读性是比较差的，我们通常会用TKPROF这个工具来格式化在个trace文件。tkprof工具是oracle自带的一个工具，用于处理原始的trace文件，它的主要作用是合并汇总trace文件中的一些项，规范化文件格式，使得文件具有可读性，下面是简单的格式化一个文件

[oracle@localhost trace]$ tkprof RBKSAFARI_ora_1302.trc new.txt



TKPROF: Release 11.2.0.1.0 - Development on Fri Oct 11 19:08:57 2013



Copyright (c) 1982, 2009, Oracle and/or its affiliates. All rights reserved.

-----------------------------------------------------

下面来执行一条sql语句使用，看看trace文件中的内容信息

SQL> select tracefile from v$process where addr=(select paddr from v$session where sid=(select distinct sid from v$mystat));



TRACEFILE

--------------------------------------------------------------------------------

/u01/app/oracle/diag/rdbms/rbksafari/RBKSAFARI/trace/RBKSAFARI_ora_30598.trc



SQL> alter session set sql_trace=true;



Session altered.



SQL> select * from test where id=100;



使用tkprof工具格式化RBKSAFARI_ora_30598.trc这个文件

[oracle@localhost trace]$ tkprof RBKSAFARI_ora_30598.trc mytrace.txt



TKPROF: Release 11.2.0.1.0 - Development on Fri Oct 11 19:17:25 2013



Copyright (c) 1982, 2009, Oracle and/or its affiliates. All rights reserved.





用vi打开如下

TKPROF: Release 11.2.0.1.0 - Development on Fri Oct 11 19:17:25 2013



Copyright (c) 1982, 2009, Oracle and/or its affiliates. All rights reserved.



Trace file: RBKSAFARI_ora_30598.trc

Sort options: default



********************************************************************************

count = number of times OCI procedure was executed

cpu = cpu time in seconds executing

elapsed = elapsed time in seconds executing

disk = number of physical reads of buffers from disk

query = number of buffers gotten for consistent read

current = number of buffers gotten in current mode (usually for update)

rows = number of rows processed by the fetch or execute call

********************************************************************************

trace文件头部的信息描述了tkprof的版本，以及报告中一些列的定义，在下面的报告中，每一条sql都包含了这条sql执行过程的所有信息，对于任何一条sql都应该包含3个步骤（对应下表的call列）

分析（parse）：SQL的分析阶段

执行（execute）：SQL的执行阶段

数据提取（Fetch）：数据提取阶段

横向的列除了call之外，还包含了一下信息：

count：计算器，表示当前的操作被执行了多少次。

cpu：当前的操作消耗的cpu时间（单位秒）

Elapsed: 当前的操作一共用时多少（包括cpu事件和等待时间）

Disk：当前操作的物理读（磁盘i/o次数）

Query：当前操作的一致性读方式读取的数据块数（通常是查询）

Current：当前操作的current的方式读取的数据库数（通常是修改数据块使用的方式）

Rows：当前操作处理的数据记录数



SQL ID: 4tk6t8tfsfqbf

Plan Hash: 0

alter session set sql_trace=true





call count cpu elapsed disk query current rows

------- ------ -------- ---------- ---------- ---------- ---------- ----------

Parse 0 0.00 0.00 0 0 0 0

Execute 1 0.00 0.00 0 0 0 0

Fetch 0 0.00 0.00 0 0 0 0

------- ------ -------- ---------- ---------- ---------- ---------- ----------

total 1 0.00 0.00 0 0 0 0



Misses in library cache during parse: 0

Misses in library cache during execute: 1

Optimizer mode: ALL_ROWS

Parsing user id: SYS

********************************************************************************



下面才是我们需要的信息，我们执行的那条查询sql的trace信息



SQL ID: d3gtqbsdqbbwq

Plan Hash: 4274779609

select *

from

test where id=100





call count cpu elapsed disk query current rows

------- ------ -------- ---------- ---------- ---------- ---------- ----------

Parse 1 0.00 0.00 0 0 0 0

Execute 1 0.00 0.00 0 0 0 0

Fetch 2 0.00 0.00 0 3 0 1

------- ------ -------- ---------- ---------- ---------- ---------- ----------

total 4 0.00 0.00 0 3 0 1



我们看到，这条sql语句被分析了一次，执行了一次，数据提取了2次，cpu和elpased都为0是因为这个表暂用的数据比较少，可以看到一共查询3个块，没有物理读，说明这个3个块已经缓冲到了buffer cache中，最后返回了1行的记录



Misses in library cache during parse: 1 --说明在shared pool中没有命中，说明这是一次硬分析

Optimizer mode: ALL_ROWS --当前优化器模式为CBO ALL_ROS

Parsing user id: SYS --分析用户id



下面是这条sql语句的具体执行计划信息，这里注意，这个执行计划里面的信息不是CBO根据表分析数据估算出的数值，而是SQL语句实际执行过程中消耗的资源信息，其中：

Rows 当前操作返回的实际返回的记录数。

Row Source Operation 表示当前操作的数据访问方式。

cr（consistent read）一致性读取的数据块，相当于query列上的fetch的值

pr（physical read）物理读取的数据块，相当于disk列上的fetch的值

pw（physical write） 物理写

time 当前操作执行时间



Rows Row Source Operation

------- ---------------------------------------------------

1 TABLE ACCESS BY INDEX ROWID TEST (cr=3 pr=0 pw=0 time=0 us cost=2 size=29 card=1)

1 INDEX UNIQUE SCAN SYS_C00399080 (cr=2 pr=0 pw=0 time=0 us cost=1 size=0 card=1)(object id 217036)





下面部分是对这个sql_trace期间所有非递归sql语句的执行信息汇总

OVERALL TOTALS FOR ALL NON-RECURSIVE STATEMENTS



call count cpu elapsed disk query current rows

------- ------ -------- ---------- ---------- ---------- ---------- ----------

Parse 1 0.00 0.00 0 0 0 0

Execute 2 0.00 0.00 0 0 0 0

Fetch 2 0.00 0.00 0 3 0 1

------- ------ -------- ---------- ---------- ---------- ---------- ----------

total 5 0.00 0.00 0 3 0 1



Misses in library cache during parse: 1

Misses in library cache during execute: 1



下面是递归调用语句的信息统计，递归sql语句是指执行一条sql语句衍生执行其它的sql，这些衍生出来的sql语句叫做递归sql语句。比如Oracle为了执行我们发出的这条sql语句

select * from test where id=100

需要对这条sql语句进行分析，需要读取一些数据字典来获取相关信息，比如是否有权限，对象是否有存在，对象的存储信息，下面统计都为0是因为这条sql语句之前已经执行过，



OVERALL TOTALS FOR ALL RECURSIVE STATEMENTS



call count cpu elapsed disk query current rows

------- ------ -------- ---------- ---------- ---------- ---------- ----------

Parse 0 0.00 0.00 0 0 0 0

Execute 0 0.00 0.00 0 0 0 0

Fetch 0 0.00 0.00 0 0 0 0

------- ------ -------- ---------- ---------- ---------- ---------- ----------

total 0 0.00 0.00 0 0 0 0



Misses in library cache during parse: 0



2 user SQL statements in session.

0 internal SQL statements in session.

2 SQL statements in session.



上面是一个通过tkprof工具处理后的结果集，它真实的统计了sql在运行过程中的各种资源消耗，这个报告对于分析性能有问题的sql语句非常重要



如果你想确切地知道sql语句的每一步执行时如何操作的，就需要分析原始的trace文件，下面给出了这条sql语句的关键部分

PARSING IN CURSOR #1 len=31 dep=0 uid=0 oct=3 lid=0 tim=1381490108569206 hv=459648918 ad='6d9c7590' sqlid='d3gtqbsdqbbwq'

select * from test where id=100

END OF STMT

PARSE #1:c=1999,e=1757,p=0,cr=0,cu=0,mis=1,r=0,dep=0,og=1,plh=4274779609,tim=1381490108569205

EXEC #1:c=0,e=41,p=0,cr=0,cu=0,mis=0,r=0,dep=0,og=1,plh=4274779609,tim=1381490108569319

FETCH #1:c=0,e=92,p=0,cr=3,cu=0,mis=0,r=1,dep=0,og=1,plh=4274779609,tim=1381490108569461

STAT #1 id=1 cnt=1 pid=0 pos=1 obj=217035 op='TABLE ACCESS BY INDEX ROWID TEST (cr=3 pr=0 pw=0 time=0 us cost=2 size=29 card=1)'

STAT #1 id=2 cnt=1 pid=1 pos=1 obj=217036 op='INDEX UNIQUE SCAN SYS_C00399080 (cr=2 pr=0 pw=0 time=0 us cost=1 size=0 card=1)'

FETCH #1:c=0,e=2,p=0,cr=0,cu=0,mis=0,r=0,dep=0,og=0,plh=4274779609,tim=1381490108591269

..........



我们看到Oracle首先对这条sql语句做分析，并且有一个游标号CURSOR #1，这个在整个trace文件中不是唯一的，当一条sql语句执行完毕后，这个号会被另外一个sql语句重用

我们还可以看到这条sql语句被分析了一次，执行了一次，fetch了2次，stat#1是对这条sql语句执行过程中的资源消耗的统计，这些输出顺序就是sql语句的执行顺序，通过这些顺序就可以了解到sql语句是如何一步一步执行的。



下面是列出了这些指标的解释

PARSING IN CURSOR 部分



len：被分析sql的长度

dep：产生递归sql的深度

uid：user id

otc：Oracle command type命令的类型

lid：私有用的id

tim：时间戳

hv： hash value

ad： sql address



PARSE，EXEC，FETCH部分：



c: 消耗的cpu time

e：elapsed time 操作的用时

p：physical reads次数

cr：consistent reads数据的块

cu：current方式读取的数据块

mis：cursor miss in canche硬分析次数

r：rows处理的行数

dep：depth递归sql的深度

og：optimize goal优化器模式

tim：timstamp时间戳



stats部分：



id：执行计划的行源号

cnt：当前行源返回的行数

pid：当前行源的父号

pos：执行计划中的位置

obj：当前操作的对象id

op：当前行源的数据访问操作



三 10046事件



10046事件按照收集信息的内容，可以分为4个级别



1）Level 1 等同于sql_trace的功能。

2）Level 4 在Level 1的基础上收集绑定变量的信息

3）level 8 在Level 1的基础上增加了等等事件的信息

4）level12 等同于Level 4 + Level *，集同时收集绑定变量和等待事件信息


可以看出level级别越高，收集的信息越全面，我们用下面例子来分别看下这几个级别的作用


（1）LEVEL 4

--首先查询trace文件路径

SQL> select tracefile from v$process where addr=(select paddr from v$session where sid=(select distinct sid from v$mystat));

TRACEFILE
--------------------------------------------------------------------------------
/u01/app/oracle/diag/rdbms/rbksafari/RBKSAFARI/trace/RBKSAFARI_ora_9493.trc

SQL> create table test as select * from dba_objects;
Table created.
SQL> exec dbms_stats.gather_table_stats('sys','test');
PL/SQL procedure successfully completed.

--设定10046事件的级别为4

SQL> alter session set events '10046 trace name context forever,level 4';

Session altered.

--定义2个变量x，y

SQL> var x number;

SQL> var y varchar2(10);

SQL> exec :x := 20;



PL/SQL procedure successfully completed.



SQL> exec :y :='TEST';



PL/SQL procedure successfully completed.



--通过绑定变量查询表

SQL> select object_id,object_name from test where object_id=:x or object_name=:y;



OBJECT_ID OBJECT_NAM

---------- ----------

20 ICOL$

221329 TEST



SQL> alter session set events '10046 trace name context off';



Session altered.



这样就完成了使用10046事件做SQL trace的工作，注意，LEVEL 4获取的绑定变量的信息只能在原始的trace文件里面获取，在通过tkprof工具格式化后是看不到的，下面是这条sql在原始文件中的关键部分



=====================

PARSING IN CURSOR #1 len=75 dep=0 uid=0 oct=3 lid=0 tim=1381546195913578 hv=2021462068 ad='74fc6200' sqlid='gkkf6ndw7u41n'

select object_id,object_name from test where object_id=:x or object_name=:y



END OF STMT

PARSE #1:c=0,e=99,p=0,cr=0,cu=0,mis=0,r=0,dep=0,og=1,plh=1357081020,tim=1381546195913577

BINDS #1:

Bind#0

oacdty=02 mxl=22(22) mxlc=00 mal=00 scl=00 pre=00

oacflg=03 fl2=1000000 frm=00 csi=00 siz=56 off=0

kxsbbbfp=7fb340f24728 bln=22 avl=02 flg=05

value=20

Bind#1

oacdty=01 mxl=32(30) mxlc=00 mal=00 scl=00 pre=00

oacflg=03 fl2=1000000 frm=01 csi=873 siz=0 off=24

kxsbbbfp=7fb340f24740 bln=32 avl=04 flg=01

value="TEST"

EXEC #1:c=0,e=139,p=0,cr=0,cu=0,mis=0,r=0,dep=0,og=1,plh=1357081020,tim=1381546195913782

FETCH #1:c=0,e=128,p=0,cr=4,cu=0,mis=0,r=1,dep=0,og=1,plh=1357081020,tim=1381546195913961

FETCH #1:c=10999,e=11326,p=0,cr=1052,cu=0,mis=0,r=1,dep=0,og=1,plh=1357081020,tim=1381546195925477

STAT #1 id=1 cnt=2 pid=0 pos=1 obj=221329 op='TABLE ACCESS FULL TEST (cr=1056 pr=0 pw=0 time=0 us cost=287 size=90 card=3)'



我们清楚的看到CURSOR #1 运行了绑定变量BINDS #1:

Bind#0 表示第一个绑定变量，最后一个value=20，表示这个变量的值为20

Bind#1 表示第二个绑定变量，最后一个value="TEST"，表示这个变量的值为TEST



2) LEVEL 8



SQL> select tracefile from v$process where addr=(select paddr from v$session where sid=(select distinct sid from v$mystat));



TRACEFILE

--------------------------------------------------------------------------------

/u01/app/oracle/diag/rdbms/rbksafari/RBKSAFARI/trace/RBKSAFARI_ora_9603.trc



--打开另外一个session 2 模拟TX - row lock contention等待事件

SQL> update test set object_id=10 where object_name='TEST';



1 row updated.





--回到这个session启用10046 level 8

SQL> alter session set events '10046 trace name context forever,level 8';



Session altered.



--更新同一行，这个时候session就被另外一个session阻塞了

SQL> update test set object_id=20 where object_name='TEST';



--等待1分钟后，在session 2 commit



SQL> commit;



Commit complete.



--回到session 通过绑定变量做查询

SQL> var x number;

SQL> var y varchar2(10);

SQL> exec :x := 20;



PL/SQL procedure successfully completed.



SQL> exec :y :='TEST';



PL/SQL procedure successfully completed.



SQL> col object_name for a10;

SQL> select object_id,object_name from test where object_id=:x or object_name=:y;



OBJECT_ID OBJECT_NAM

---------- ----------

20 ICOL$

20 TEST



SQL> alter session set events '10046 trace name context off';



Session altered.





下面是等待事件相关的类容

PARSING IN CURSOR #1 len=53 dep=0 uid=0 oct=6 lid=0 tim=1381547023329562 hv=902883731 ad='763c0908' sqlid='fm3f2m0ux1ucm'

update test set object_id=20 where object_name='TEST'

END OF STMT

PARSE #1:c=2000,e=2079,p=0,cr=0,cu=0,mis=1,r=0,dep=0,og=1,plh=839355234,tim=1381547023329561



*** 2013-10-12 11:04:37.870

WAIT #1: nam='enq: TX - row lock contention' ela= 54462429 name|mode=1415053318 usn<<16 | slot=65562 sequence=9055 obj#=221329 tim=1381547077870603

EXEC #1:c=10997,e=54541351,p=0,cr=1057,cu=4,mis=0,r=1,dep=0,og=1,plh=839355234,tim=1381547077870989

STAT #1 id=1 cnt=0 pid=0 pos=1 obj=0 op='UPDATE TEST (cr=1057 pr=0 pw=0 time=0 us)'

STAT #1 id=2 cnt=1 pid=1 pos=1 obj=221329 op='TABLE ACCESS FULL TEST (cr=1056 pr=0 pw=0 time=0 us cost=287 size=60 card=2)'

WAIT #1: nam='SQL*Net message to client' ela= 4 driver id=1650815232 #bytes=1 p3=0 obj#=-1 tim=1381547077871119



*** 2013-10-12 11:05:14.205

WAIT #1: nam='SQL*Net message from client' ela= 36334185 driver id=1650815232 #bytes=1 p3=0 obj#=-1 tim=1381547114205328

CLOSE #1:c=0,e=16,dep=0,type=0,tim=1381547114206287



可以清楚的看到在CURSOR #1有刚才模拟的等待事件enq: TX - row lock contention



下面是select的相关内容

PARSING IN CURSOR #6 len=75 dep=0 uid=0 oct=3 lid=0 tim=1381548130797812 hv=2021462068 ad='74fc6200' sqlid='gkkf6ndw7u41n'

select object_id,object_name from test where object_id=:x or object_name=:y

END OF STMT

PARSE #6:c=0,e=299,p=0,cr=0,cu=0,mis=1,r=0,dep=0,og=1,plh=0,tim=1381548130797811

EXEC #6:c=1000,e=751,p=0,cr=0,cu=0,mis=1,r=0,dep=0,og=1,plh=1357081020,tim=1381548130798637

WAIT #6: nam='SQL*Net message to client' ela= 5 driver id=1650815232 #bytes=1 p3=0 obj#=0 tim=1381548130798688

FETCH #6:c=0,e=48,p=0,cr=4,cu=0,mis=0,r=1,dep=0,og=1,plh=1357081020,tim=1381548130798766

WAIT #6: nam='SQL*Net message from client' ela= 180 driver id=1650815232 #bytes=1 p3=0 obj#=0 tim=1381548130798986

WAIT #6: nam='SQL*Net message to client' ela= 8 driver id=1650815232 #bytes=1 p3=0 obj#=0 tim=1381548130807489

FETCH #6:c=8999,e=8627,p=0,cr=1052,cu=0,mis=0,r=1,dep=0,og=1,plh=1357081020,tim=1381548130807646

STAT #6 id=1 cnt=2 pid=0 pos=1 obj=221329 op='TABLE ACCESS FULL TEST (cr=1056 pr=0 pw=0 time=0 us cost=287 size=90 card=3)'



可以看出LEVEL 8 里面没有包含绑定变量的信息



3) LEVEL 12

操作步骤和LEVEL 8 一样



下面是等待事件信息

PARSING IN CURSOR #1 len=53 dep=0 uid=0 oct=6 lid=0 tim=1381548626071590 hv=3865385185 ad='7011bc50' sqlid='a30698vm6a671'

update test set object_id=10 where object_name='TEST'

END OF STMT

PARSE #1:c=0,e=1147,p=0,cr=0,cu=0,mis=1,r=0,dep=0,og=1,plh=839355234,tim=1381548626071589



*** 2013-10-12 11:30:46.006

WAIT #1: nam='enq: TX - row lock contention' ela= 19930521 name|mode=1415053318 usn<<16 | slot=131092 sequence=12176 obj#=221329 tim=1381548646006494

EXEC #1:c=4999,e=19935098,p=0,cr=1057,cu=4,mis=0,r=1,dep=0,og=1,plh=839355234,tim=1381548646006763

STAT #1 id=1 cnt=0 pid=0 pos=1 obj=0 op='UPDATE TEST (cr=1057 pr=0 pw=0 time=0 us)'

STAT #1 id=2 cnt=1 pid=1 pos=1 obj=221329 op='TABLE ACCESS FULL TEST (cr=1056 pr=0 pw=0 time=0 us cost=287 size=60 card=2)'

WAIT #1: nam='SQL*Net message to client' ela= 4 driver id=1650815232 #bytes=1 p3=0 obj#=-1 tim=1381548646006926



*** 2013-10-12 11:30:49.893

WAIT #1: nam='SQL*Net message from client' ela= 3886351 driver id=1650815232 #bytes=1 p3=0 obj#=-1 tim=1381548649893302



下面是绑定变量信息

PARSING IN CURSOR #1 len=75 dep=0 uid=0 oct=3 lid=0 tim=1381548682580601 hv=2021462068 ad='74fc6200' sqlid='gkkf6ndw7u41n'

select object_id,object_name from test where object_id=:x or object_name=:y

END OF STMT

PARSE #1:c=0,e=74,p=0,cr=0,cu=0,mis=0,r=0,dep=0,og=1,plh=1357081020,tim=1381548682580599

BINDS #1:

Bind#0

oacdty=02 mxl=22(22) mxlc=00 mal=00 scl=00 pre=00

oacflg=03 fl2=1000000 frm=00 csi=00 siz=56 off=0

kxsbbbfp=7f1296db5758 bln=22 avl=02 flg=05

value=20

Bind#1

oacdty=01 mxl=32(30) mxlc=00 mal=00 scl=00 pre=00

oacflg=03 fl2=1000000 frm=01 csi=873 siz=0 off=24

kxsbbbfp=7f1296db5770 bln=32 avl=04 flg=01

value="TEST"

EXEC #1:c=0,e=137,p=0,cr=0,cu=0,mis=0,r=0,dep=0,og=1,plh=1357081020,tim=1381548682580809



可以看出LEVEL 14 = LEVEL 4 + LEVEL 8



如同SQL_TRACE如果跟踪其它session，同样在获取session的sid和serial后即可通过下面的命令跟踪

SQL> exec dbms_monitor.session_trace_enable(100,11110,waits=>true,bind=>true); --启用



SQL> exec dbms_monitor.session_trace_disable(100,11110); --禁用





另外启用10046事件受下面两个参数的影响



SQL> show parameter timed_statistics



NAME TYPE VALUE

------------------------------------ ----------- ------------------------------

timed_statistics boolean TRUE

SQL> show parameter max_dump_file_size;



NAME TYPE VALUE

------------------------------------ ----------- ------------------------------

max_dump_file_size string unlimited



在oracle 10g后在两个参数保存默认就好了，简单知道就可以了。

select * from v$session where 

where SID=431;

alter session  set events='1438 trace name Errorstack forever,level 10';

show parameter user_dump_dest;  --/home/oracle/diag/rdbms/tjessl6db/tjessl6db/trace 
select tracefile from v$process where addr=(select paddr from v$session where sid=431);
/home/oracle/diag/rdbms/tjessl6db/tjessl6db/trace/tjessl6db_ora_3200.trc
PVIStation.exe DESKTOP-1CTVIVL

execute dbms_system.set_sql_trace_in_session(431,12741,true);
execute dbms_system.set_sql_trace_in_session(144,9053,false);

alter system set events='1438 trace name errorstack forever,level 3';
alter system set events='1438 trace name errorstack off';

alter system set events='1438 trace name Errorstack off';
ftpuser

oracle XE 导入分区表
原创mood0369 最后发布于2017-05-12 15:20:49 阅读数 278  收藏
展开
使用impdp向oracle xe导入分区表采用分区选项参数：

impdp system/******@XE directory='dir_dp' dumpfile='parttables_.dmp' logfile='parttables.log' partition_options=merge  TABLE_EXISTS_ACTION=REPLACE



数据泵参数partition_options 在对于迁移分区表的使用。


1、NONE 象在系统上的分区表一样创建。
2、DEPARTITION 每个分区表和子分区表作为一个独立的表创建，名字使用表和分区（子分区）名字的组合。
3、MERGE 将所有分区合并到一个表
————————————————
版权声明：本文为CSDN博主「mood0369」的原创文章，遵循 CC 4.0 BY-SA 版权协议，转载请附上原文出处链接及本声明。
原文链接：https://blog.csdn.net/mood0369/java/article/details/71732869

