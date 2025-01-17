﻿using System;

namespace Prowl.Runtime
{

    // Taken and modified from Duality's ContentRef.cs
    // https://github.com/AdamsLair/duality/blob/master/Source/Core/Duality/ContentRef.cs

    public struct AssetRef<T> : IAssetRef, ISerializable where T : EngineObject
    {
        private T? instance;
        private Guid assetID = Guid.Empty;
        private short fileID = 0;

        /// <summary>
		/// The actual <see cref="EngineObject"/>. If currently unavailable, it is loaded and then returned.
		/// Because of that, this Property is only null if the references Resource is missing, invalid, or
		/// this content reference has been explicitly set to null. Never returns disposed Resources.
		/// </summary>
		public T? Res
        {
            get
            {
                if (instance == null || instance.IsDestroyed) RetrieveInstance();
                return instance;
            }
            set
            {
                assetID = value == null ? Guid.Empty : value.AssetID;
                fileID = value == null ? (short)0 : value.FileID;
                instance = value;
            }
        }

        /// <summary>
        /// Returns the current reference to the Resource that is stored locally. No attemp is made to load or reload
        /// the Resource if currently unavailable.
        /// </summary>
        public T? ResWeak
        {
            get { return instance == null || instance.IsDestroyed ? null : instance; }
        }

        /// <summary>
        /// The path where to look for the Resource, if it is currently unavailable.
        /// </summary>
        public Guid AssetID
        {
            get { return assetID; }
            set
            {
                assetID = value;
                if (instance != null && instance.AssetID != value)
                    instance = null;
            }
        }

        /// <summary>
        /// The Asset index inside the asset file. 0 is the Main Asset
        /// </summary>
        public short FileID {
            get => fileID;
            set => fileID = value;
        }


        /// <summary>
        /// Returns whether this content reference has been explicitly set to null.
        /// </summary>
        public bool IsExplicitNull
        {
            get
            {
                return instance == null && assetID == Guid.Empty;
            }
        }

        /// <summary>
        /// Returns whether this content reference is available in general. This may trigger loading it, if currently unavailable.
        /// </summary>
        public bool IsAvailable
        {
            get
            {
                if (instance != null && !instance.IsDestroyed) return true;
                RetrieveInstance();
                return instance != null;
            }
        }

        /// <summary>
        /// Returns whether the referenced Resource is currently loaded.
        /// </summary>
        public bool IsLoaded
        {
            get
            {
                if (instance != null && !instance.IsDestroyed) return true;
                return Application.AssetProvider.HasAsset(assetID);
            }
        }

        /// <summary>
        /// Returns whether the Resource has been generated at runtime and cannot be retrieved via content path.
        /// </summary>
        public bool IsRuntimeResource
        {
            get { return instance != null && assetID == Guid.Empty; }
        }

        public string Name
        {
            get
            {
                if (instance != null) return instance.IsDestroyed ? "DESTROYED_" + instance.Name : instance.Name;
                return "No Instance";
            }
        }

        public Type InstanceType => typeof(T);

        /// <summary>
        /// Creates a ContentRef pointing to the <see cref="Resource"/> at the specified id / using 
        /// the specified alias.
        /// </summary>
        /// <param name="id"></param>
        public AssetRef(Guid id)
        {
            instance = null;
            assetID = id;
        }
        /// <summary>
        /// Creates a ContentRef pointing to the specified <see cref="Resource"/>.
        /// </summary>
        /// <param name="res">The Resource to reference.</param>
        public AssetRef(T res)
        {
            instance = res;
            assetID = res != null ? res.AssetID : Guid.Empty;
            fileID = res != null ? res.FileID : (short)0;
        }

        public object? GetInstance()
        {
            return Res;
        }

        /// <summary>
        /// Loads the associated content as if it was accessed now.
        /// You don't usually need to call this method. It is invoked implicitly by trying to 
        /// access the <see cref="AssetRef{T}"/>.
        /// </summary>
        public void EnsureLoaded()
        {
            if (instance == null || instance.IsDestroyed)
                RetrieveInstance();
        }
        /// <summary>
        /// Discards the resolved content reference cache to allow garbage-collecting the Resource
        /// without losing its reference. Accessing it will result in reloading the Resource.
        /// </summary>
        public void Detach()
        {
            instance = null;
        }

        private void RetrieveInstance()
        {
            if (assetID != Guid.Empty)
                instance = Application.AssetProvider.LoadAsset<T>(this);
            else if (instance != null && instance.AssetID != Guid.Empty)
                instance = Application.AssetProvider.LoadAsset<T>(instance.AssetID, instance.FileID);
            else
                instance = null;
        }

        public override string ToString()
        {
            Type resType = typeof(T);

            char stateChar;
            if (IsRuntimeResource)
                stateChar = 'R';
            else if (IsExplicitNull)
                stateChar = 'N';
            else if (IsLoaded)
                stateChar = 'L';
            else
                stateChar = '_';

            return string.Format("[{2}] {0}", resType.Name, stateChar);
        }

        public override bool Equals(object obj)
        {
            if (obj is AssetRef<T>)
                return this == (AssetRef<T>)obj;
            else
                return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (assetID != Guid.Empty) return assetID.GetHashCode() + fileID.GetHashCode();
            else if (instance != null) return instance.GetHashCode();
            else return 0;
        }

        public bool Equals(AssetRef<T> other)
        {
            return this == other;
        }

        public static implicit operator AssetRef<T>(T res)
        {
            return new AssetRef<T>(res);
        }
        public static explicit operator T(AssetRef<T> res)
        {
            return res.Res;
        }

        /// <summary>
        /// Compares two AssetRefs for equality.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <remarks>
        /// This is a two-step comparison. First, their actual Resources references are compared.
        /// If they're both not null and equal, true is returned. Otherwise, their AssetID's are compared for equality
        /// </remarks>
        public static bool operator ==(AssetRef<T> first, AssetRef<T> second)
        {
            // Old check, didn't work for XY == null when XY was a Resource created at runtime
            //if (first.instance != null && second.instance != null)
            //    return first.instance == second.instance;
            //else
            //    return first.assetID == second.assetID;

            // Completely identical
            if (first.instance == second.instance && first.assetID == second.assetID)
                return true;
            // Same instances
            else if (first.instance != null && second.instance != null)
                return first.instance == second.instance;
            // Null checks
            else if (first.IsExplicitNull) return second.IsExplicitNull;
            else if (second.IsExplicitNull) return first.IsExplicitNull;
            // Path comparison
            else
            {
                Guid? firstPath = first.instance != null ? first.instance.AssetID : first.assetID;
                Guid? secondPath = second.instance != null ? second.instance.AssetID : second.assetID;
                return firstPath == secondPath && first.fileID == second.fileID;
            }
        }
        /// <summary>
        /// Compares two AssetRefs for inequality.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        public static bool operator !=(AssetRef<T> first, AssetRef<T> second)
        {
            return !(first == second);
        }


        public CompoundTag Serialize(TagSerializer.SerializationContext ctx)
        {
            CompoundTag compoundTag = new CompoundTag();
            compoundTag.Add("AssetID", new StringTag(assetID.ToString()));
            if(fileID != 0)
                compoundTag.Add("FileID", new ShortTag(fileID));
            if (IsRuntimeResource)
                compoundTag.Add("Instance", TagSerializer.Serialize(instance, ctx));
            return compoundTag;
        }

        public void Deserialize(CompoundTag value, TagSerializer.SerializationContext ctx)
        {
            assetID = Guid.Parse(value["AssetID"].StringValue);
            fileID = value.TryGet("FileID", out ShortTag fileTag) ? fileTag.ShortValue : (short)0;
            if (assetID == Guid.Empty && value.TryGet("Instance", out CompoundTag tag))
                instance = TagSerializer.Deserialize<T?>(tag, ctx);
        }
    }
}
