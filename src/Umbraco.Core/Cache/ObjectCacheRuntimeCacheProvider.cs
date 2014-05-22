﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.Caching;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Caching;
using Umbraco.Core.Logging;
using CacheItemPriority = System.Web.Caching.CacheItemPriority;

namespace Umbraco.Core.Cache
{
    /// <summary>
    /// A cache provider that wraps the logic of a System.Runtime.Caching.ObjectCache
    /// </summary>
    internal class ObjectCacheRuntimeCacheProvider : IRuntimeCacheProvider
    {
        private readonly ReaderWriterLockSlim _locker = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        internal ObjectCache MemoryCache;

        public ObjectCacheRuntimeCacheProvider()
        {
            MemoryCache = new MemoryCache("in-memory");
        }

        public virtual void ClearAllCache()
        {
            using (new WriteLock(_locker))
            {
                MemoryCache.DisposeIfDisposable();
                MemoryCache = new MemoryCache("in-memory");
            }
        }

        public virtual void ClearCacheItem(string key)
        {
            using (new WriteLock(_locker))
            {
                if (MemoryCache[key] == null) return;
                MemoryCache.Remove(key);
            }            
        }

        public virtual void ClearCacheObjectTypes(string typeName)
        {
            using (new WriteLock(_locker))
            {
                var keysToRemove = MemoryCache
                    .Where(c => c.Value != null && c.Value.GetType().ToString().InvariantEquals(typeName))
                    .Select(c => c.Key)
                    .ToArray();
                foreach (var k in keysToRemove)
                    MemoryCache.Remove(k);
            }
        }

        public virtual void ClearCacheObjectTypes<T>()
        {
            using (new WriteLock(_locker))
            {
                var typeOfT = typeof (T);
                var keysToRemove = MemoryCache
                    .Where(c => c.Value != null && c.Value.GetType() == typeOfT)
                    .Select(c => c.Key)
                    .ToArray();
                foreach (var k in keysToRemove)
                    MemoryCache.Remove(k);
            }
        }

        public virtual void ClearCacheObjectTypes<T>(Func<string, T, bool> predicate)
        {
            using (new WriteLock(_locker))
            {
                var typeOfT = typeof(T);
                var keysToRemove = MemoryCache
                    .Where(c => c.Value != null && c.Value.GetType() == typeOfT && predicate(c.Key, (T)c.Value))
                    .Select(c => c.Key)
                    .ToArray();
                foreach (var k in keysToRemove)
                    MemoryCache.Remove(k);
            }
        }

        public virtual void ClearCacheByKeySearch(string keyStartsWith)
        {
            using (new WriteLock(_locker))
            {
                var keysToRemove = (from c in MemoryCache where c.Key.InvariantStartsWith(keyStartsWith) select c.Key).ToList();
                foreach (var k in keysToRemove)
                {
                    MemoryCache.Remove(k);
                }
            }            
        }

        public virtual void ClearCacheByKeyExpression(string regexString)
        {
            using (new WriteLock(_locker))
            {
                var keysToRemove = (from c in MemoryCache where Regex.IsMatch(c.Key, regexString) select c.Key).ToList();
                foreach (var k in keysToRemove)
                {
                    MemoryCache.Remove(k);
                }
            }     
        }

        public virtual IEnumerable<object> GetCacheItemsByKeySearch(string keyStartsWith)
        {
            using (new ReadLock(_locker))
            {
                return MemoryCache
                    .Where(x => x.Key.InvariantStartsWith(keyStartsWith))
                    .Select(x => ((Lazy<object>) x.Value).Value)
                    .ToList();
            }
        }

        public IEnumerable<object> GetCacheItemsByKeyExpression(string regexString)
        {
            using (new ReadLock(_locker))
            {
                return MemoryCache
                    .Where(x => Regex.IsMatch(x.Key, regexString))
                    .Select(x => ((Lazy<object>) x.Value).Value)
                    .ToList();
            }
        }

        public virtual object GetCacheItem(string cacheKey)
        {
            using (new ReadLock(_locker))
            {
                var result = MemoryCache.Get(cacheKey);
                return result;
            }
        }

        public virtual object GetCacheItem(string cacheKey, Func<object> getCacheItem)
        {
            return GetCacheItem(cacheKey, getCacheItem, null);
        }

        public object GetCacheItem(
            string cacheKey, 
            Func<object> getCacheItem, 
            TimeSpan? timeout, 
            bool isSliding = false, 
            CacheItemPriority priority = CacheItemPriority.Normal,
            CacheItemRemovedCallback removedCallback = null, 
            string[] dependentFiles = null)
        {
            // see notes in HttpRuntimeCacheProvider

            Lazy<object> result;

            using (var lck = new UpgradeableReadLock(_locker))
            {
                result = MemoryCache.Get(cacheKey) as Lazy<object>;
                if (result == null /* || (result.IsValueCreated && result.Value == null) */)
                {
                    lck.UpgradeToWriteLock();

                    result = new Lazy<object>(getCacheItem);
                    var policy = GetPolicy(timeout, isSliding, removedCallback, dependentFiles);
                    MemoryCache.Set(cacheKey, result, policy);
                }
            }

            return result.Value;
        }

        public void InsertCacheItem(string cacheKey, Func<object> getCacheItem, TimeSpan? timeout = null, bool isSliding = false, CacheItemPriority priority = CacheItemPriority.Normal, CacheItemRemovedCallback removedCallback = null, string[] dependentFiles = null)
        {
            // see notes in HttpRuntimeCacheProvider

            var result = new Lazy<object>(getCacheItem);
            var value = result.Value; // force evaluation now
            //if (value == null) return;

            var policy = GetPolicy(timeout, isSliding, removedCallback, dependentFiles);
            MemoryCache.Set(cacheKey, result, policy);
        }

        private static CacheItemPolicy GetPolicy(TimeSpan? timeout = null, bool isSliding = false, CacheItemRemovedCallback removedCallback = null, string[] dependentFiles = null)
        {
            var absolute = isSliding ? ObjectCache.InfiniteAbsoluteExpiration : (timeout == null ? ObjectCache.InfiniteAbsoluteExpiration : DateTime.Now.Add(timeout.Value));
            var sliding = isSliding == false ? ObjectCache.NoSlidingExpiration : (timeout ?? ObjectCache.NoSlidingExpiration);

            var policy = new CacheItemPolicy
            {
                AbsoluteExpiration = absolute,
                SlidingExpiration = sliding
            };

            if (dependentFiles != null && dependentFiles.Any())
            {
                policy.ChangeMonitors.Add(new HostFileChangeMonitor(dependentFiles.ToList()));
            }
            
            if (removedCallback != null)
            {
                policy.RemovedCallback = arguments =>
                {
                    //convert the reason
                    var reason = CacheItemRemovedReason.Removed;
                    switch (arguments.RemovedReason)
                    {
                        case CacheEntryRemovedReason.Removed:
                            reason = CacheItemRemovedReason.Removed;
                            break;
                        case CacheEntryRemovedReason.Expired:
                            reason = CacheItemRemovedReason.Expired;
                            break;
                        case CacheEntryRemovedReason.Evicted:
                            reason = CacheItemRemovedReason.Underused;
                            break;
                        case CacheEntryRemovedReason.ChangeMonitorChanged:
                            reason = CacheItemRemovedReason.Expired;
                            break;
                        case CacheEntryRemovedReason.CacheSpecificEviction:
                            reason = CacheItemRemovedReason.Underused;
                            break;
                    }
                    //call the callback
                    removedCallback(arguments.CacheItem.Key, arguments.CacheItem.Value, reason);
                };
            }
            return policy;
        }
        
    }
}