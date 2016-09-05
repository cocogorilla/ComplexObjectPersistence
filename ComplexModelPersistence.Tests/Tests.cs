using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using ComplexObjectPersistence;
using Dapper;
using Ploeh.AutoFixture.Xunit2;
using Xunit;

namespace ComplexModelPersistence.Tests
{
    public class Tests : IClassFixture<DbSetup>
    {
        /// <summary>
        /// Generate a random set of input models and execute a merge stored procedure
        /// Demonstrate that, at least for inserts, data is persited correctly.
        /// 
        /// Database internal storage should cluster and relate across integers.
        /// GUID key relations should remain logically corerct.
        /// </summary>
        /// <param name="input"></param>
        [Theory, AutoData]
        public void EndToEndTest(IEnumerable<ComplexModel> input)
        {
            using (var conn = new SqlConnection(Config.ConnectionString))
            {
                // setup a parameter matching mastermodellist table type
                var masters = input.Select(x => new
                {
                    masterid = x.MasterId,
                    mastervalue = x.MasterValue,
                    masteralternatevalue = x.MasterAlternateValue
                }).AsTableValuedParameter("mastermodellist", new[] { "masterid", "mastervalue", "masteralternatevalue" });
                // setup a parameter matching innermodellist table type (add master key to relation)
                var inners =
                    (from a in input
                     from b in a.InnerModels
                     select new
                     {
                         innerid = b.InnerId,
                         relationid = a.MasterId,
                         innerkey = b.InnerKey,
                         innervalue = b.InnerValue
                     });
                var innersDto = inners.AsTableValuedParameter("innermodellist", new[] { "innerid", "relationid", "innervalue", "innerkey" });

                // execute the mergemodels sproc with parameters
                conn.Execute("mergemodels", new
                {
                    masters,
                    inners = innersDto
                }, commandType: CommandType.StoredProcedure);

                // get actaul master values
                var mastervalues =
                    conn.Query(
                            "select masterid, mastervalue, masteralternatevalue from mastertable order by masterid")
                        .OrderBy(x => x.masterid)
                        .ToArray();

                // get actual inner values
                var innervalues =
                    conn.Query(
                            "select innerid, innervalue, innerkey from innertable order by innerid")
                        .OrderBy(x => x.innerid)
                        .ToArray();

                // get actual relations
                var relations =
                    conn.Query(
                            "select innerid, masterid from innertable a join mastertable b on a.masteruid = b.uid")
                        .OrderBy(x => x.innerid)
                        .ToArray();

                // verify master values
                input
                    .OrderBy(x => x.MasterId)
                    .Iter((x, y) =>
                    {
                        Assert.Equal(mastervalues[y].masterid, x.MasterId);
                        Assert.Equal(mastervalues[y].mastervalue, x.MasterValue);
                        Assert.Equal(mastervalues[y].masteralternatevalue, x.MasterAlternateValue);
                    });

                // verify inner values
                input
                    .SelectMany(x => x.InnerModels)
                    .OrderBy(x => x.InnerId)
                    .Iter((x, y) =>
                    {
                        Assert.Equal(innervalues[y].innerid, x.InnerId);
                        Assert.Equal(innervalues[y].innerkey, x.InnerKey);
                        Assert.Equal(innervalues[y].innervalue, x.InnerValue);
                    });

                // verify relations are correct
                inners
                    .OrderBy(x => x.innerid)
                    .Iter((x, y) =>
                    {
                        Assert.Equal(x.innerid, relations[y].innerid);
                        Assert.Equal(x.relationid, relations[y].masterid);
                    });
            }
        }
    }
}
