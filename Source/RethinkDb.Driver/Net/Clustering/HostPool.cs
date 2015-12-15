﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RethinkDb.Driver.Ast;

namespace RethinkDb.Driver.Net.Clustering
{
    public abstract class HostPool : IPoolingStrategy
    {
        protected TimeSpan RetryDelayInitial;
        protected TimeSpan RetryDelayMax;

        //this array should be monotonically increasing, to avoid 
        //unnecessary thread locking and synch problems when iterating
        //over the length of the list.
        protected HostEntry[] hostList;

        public HostPool(TimeSpan? retryDelayInitial, TimeSpan? retryDelayMax)
        {
            this.RetryDelayInitial = retryDelayInitial ?? TimeSpan.FromSeconds(30);
            this.RetryDelayMax = retryDelayMax ?? TimeSpan.FromSeconds(900);
            hostList = new HostEntry[0];
        }

        protected object hostLock = new object();

        public HostEntry[] HostList => hostList;

        public virtual void AddHost(string host, Connection conn)
        {
            lock( hostLock )
            {
                if( shuttingDown ) return;

                var oldHostList = this.hostList;
                var nextHostList = new HostEntry[oldHostList.Length + 1];
                Array.Copy(oldHostList, nextHostList, oldHostList.Length);

                //add new host to the end of the array. Initially, start off as a dead
                //host.
                var he = new HostEntry(host)
                    {
                        conn = conn,
                        Dead = true,
                        RetryDelayInitial = RetryDelayInitial,
                        RetryDelayMax = RetryDelayMax
                    };
                nextHostList[nextHostList.Length - 1] = he;
                this.hostList = nextHostList;
            }
        }

        protected bool shuttingDown = false;

        public virtual void Shutdown()
        {
            lock( hostLock )
            {
                shuttingDown = true;
            }
        }

        public abstract Task<dynamic> RunAsync<T>(ReqlAst term, object globalOpts);
        public abstract Task<Cursor<T>> RunCursorAsync<T>(ReqlAst term, object globalOpts);
        public abstract Task<T> RunAtomAsync<T>(ReqlAst term, object globalOpts);
        public abstract void RunNoReply(ReqlAst term, object globalOpts);
    }
}