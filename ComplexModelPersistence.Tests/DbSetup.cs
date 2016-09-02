using System.Data.SqlClient;
using Dapper;

namespace ComplexModelPersistence.Tests
{
    public class DbSetup
    {
        public DbSetup()
        {
            using (var conn = new SqlConnection(Config.ConnectionString))
            {
                conn.Execute(@"
if exists (select * from sys.tables where name = 'innertable') begin drop table innertable; end
if exists (select * from sys.tables where name = 'mastertable') begin drop table mastertable; end

create table mastertable (
  uid int identity(1,1) primary key,
  masterid uniqueidentifier not null,
  mastervalue varchar(80) not null,
  masteralternatevalue varchar(80));

create table innertable (
  uid int not null identity(1,1) primary key nonclustered,
  masteruid int not null foreign key references mastertable(uid),
  innerid uniqueidentifier not null,
  innervalue varchar(80) not null,
  innerkey int not null);");

                conn.Execute(@"
if exists (select * from sys.procedures where name = 'mergemodels') begin drop procedure mergemodels; end
if exists (select * from sys.types where name = 'mastermodellist') begin drop type mastermodellist; end
if exists (select * from sys.types where name = 'innermodellist') begin drop type innermodellist; end");
                conn.Execute(@"
create type mastermodellist 
as table (
  masterid uniqueidentifier primary key,
  mastervalue varchar(80) not null,
  masteralternatevalue varchar(80) not null);");
                conn.Execute(@"
create type innermodellist
as table (
  innerid uniqueidentifier primary key,
  relationid uniqueidentifier,
  innervalue varchar(80) not null,
  innerkey int not null);");
                conn.Execute(@"
create procedure mergemodels (
  @masters dbo.mastermodellist readonly,
  @inners dbo.innermodellist readonly)
as
begin
  declare @relations table (uid int primary key, masterid uniqueidentifier);

  merge mastertable T
  using @masters S
     on T.masterid = S.masterid
   when matched 
   then update 
           set T.masterid = S.masterid
             , T.mastervalue = S.mastervalue
             , T.masteralternatevalue = S.masteralternatevalue
   when not matched
   then insert (masterid, mastervalue, masteralternatevalue) 
        values (S.masterid, S.mastervalue, S.masteralternatevalue)
 output inserted.uid, inserted.masterid into @relations;

  merge innertable T
  using (select a.uid, a.masterid, b.innerid, b.innervalue, b.innerkey
           from @relations a 
           join @inners b on a.masterid = b.relationid) S
     on T.innerid = S.innerid
   when matched
   then update
           set T.innervalue = S.innervalue
             , T.innerkey = S.innerkey
             , T.masteruid = S.uid
   when not matched
   then insert (innerid, masteruid, innervalue, innerkey)
        values (S.innerid, S.uid, S.innervalue, S.innerkey);
end");
            }
        }
    }
}