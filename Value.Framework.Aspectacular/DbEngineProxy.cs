﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Aspectacular
{
    /// <summary>
    /// Base class for proxies dealing with EF, ADO.NET and other connection-based data access engines.
    /// </summary>
    /// <typeparam name="TDbEngine"></typeparam>
    public abstract class DbEngineProxy<TDbEngine> : AllocateRunDisposeProxy<TDbEngine>, IEfCallInterceptor, IStorageCommandRunner<TDbEngine>
        where TDbEngine : class, IDisposable, new()
    {
        /// <summary>
        /// If true, adds SqlUtils.SqlConnectionAttributes after establishing SQl Server connection.
        /// </summary>
        public static volatile bool UseSqlConnectionModifiers = true;

        public DbEngineProxy(IEnumerable<Aspect> aspects)
            : base(aspects)
        {
        }

        /// <summary>
        /// A pass-through Proxy constructor that creates Proxy which won't clean up instance after method invocation.
        /// </summary>
        /// <param name="dbContext"></param>
        /// <param name="aspects"></param>
        public DbEngineProxy(TDbEngine dbContext, IEnumerable<Aspect> aspects)
            : base(dbContext, aspects)
        {
        }

        #region Base class overrides

        protected override void InvokeActualInterceptedMethod(Action interceptedMethodClosure)
        {
            base.InvokeActualInterceptedMethod(interceptedMethodClosure);
            this.SaveChanges();
        }

        protected override void Step_2_BeforeTryingMethodExec()
        {
            this.ModifySqlConnectionAttributesIfApplies();

            base.Step_2_BeforeTryingMethodExec();
        }

        private void ModifySqlConnectionAttributesIfApplies()
        {
            if (UseSqlConnectionModifiers && SqlUtils.SqlConnectionAttributes != null)
            {
                SqlConnection sqlConnection = this.GetSqlConnection();
                if (sqlConnection != null)
                    sqlConnection.AttachSqlConnectionAttribs();
            }
        }

        #endregion Base class overrides

        #region Implementation of IEfCallInterceptor

        public int SaveChangeReturnValue { get; set; }

        public int SaveChanges()
        {
            if (this.AugmentedClassInstance == null) // SaveChanges() called directly, like db.GetDbProxy().SaveChanges();
                return this.ExecuteCommand(db => DbEngineProxy<TDbEngine>.SaveChangesDirect());

            this.SaveChangeReturnValue = this.CommitChanges();

            this.LogInformationData("SaveChanges() result", this.SaveChangeReturnValue);

            //if (this.InterceptedCallMetaData.MethodReturnType.Equals(typeof(void)))
            //    this.ReturnedValue = this.SaveChangeReturnValue;

            return this.SaveChangeReturnValue;
        }

        #endregion Implementation of IEfCallInterceptor

        #region Implementation of IStorageCommandRunner

        /// <summary>
        /// Command that returns no value except for int returned by underlying DB engine.
        /// </summary>
        /// <param name="callExpression"></param>
        /// <returns></returns>
        public virtual int ExecuteCommand(Expression<Action<TDbEngine>> callExpression)
        {
            this.Invoke(callExpression);
            return this.SaveChangeReturnValue;
        }

        #endregion IStorageCommandRunner

        /// <summary>
        /// Subclasses' implementation of CommitChanges() should call DbContext.SaveChanges().
        /// </summary>
        /// <returns></returns>
        public abstract int CommitChanges();

        /// <summary>
        /// Override in subclasses if they talk to SQL Server, to improve SQL command/query performance.
        /// </summary>
        /// <returns></returns>
        protected virtual SqlConnection GetSqlConnection()
        {
            return null;
        }

        #region Utility Methods

        /// <summary>
        /// Do-nothing method that facilitates calling db.GetDbProxy().SaveChanges();
        /// </summary>
        private static void SaveChangesDirect()
        {
        }

        #endregion Utility Methods
    }

    //public class AdoNetProxy<TDbConnection> : DbEngineProxy<TDbConnection>
    //    where TDbConnection : class, IDbConnection, new()
    //{
    //    public IDbCommand DbCommand { get; protected set; }

    //    public AdoNetProxy(IEnumerable<Aspect> aspects)
    //        : base(aspects)
    //    {
    //    }

    //    public AdoNetProxy(TDbConnection dbConnection, IEnumerable<Aspect> aspects)
    //        : base(dbConnection, aspects)
    //    {
    //    }

    //    public int ExecuteCommand(CommandType cmdType, string cmdText, params IDbDataParameter[] args)
    //    {
    //        this.CreateDbCommand(cmdType, cmdText, args);

    //        int retVal = this.DbCommand.ExecuteNonQuery();
    //        return retVal;
    //    }

    //    private void CreateDbCommand(CommandType cmdType, string cmdText, IDbDataParameter[] args)
    //    {
    //        if (cmdText.IsBlank())
    //            throw new ArgumentNullException("cmdText");

    //        this.DbCommand = this.AugmentedClassInstance.CreateCommand();
    //        this.DbCommand.CommandType = cmdType;
    //        this.DbCommand.CommandText = cmdText;

    //        args.ForEach(parm => this.DbCommand.Parameters.Add(parm));
    //    }
    //}
}
