﻿//
// Copyright 2020 Google LLC
//
// Licensed to the Apache Software Foundation (ASF) under one
// or more contributor license agreements.  See the NOTICE file
// distributed with this work for additional information
// regarding copyright ownership.  The ASF licenses this file
// to you under the Apache License, Version 2.0 (the
// "License"); you may not use this file except in compliance
// with the License.  You may obtain a copy of the License at
// 
//   http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing,
// software distributed under the License is distributed on an
// "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
// KIND, either express or implied.  See the License for the
// specific language governing permissions and limitations
// under the License.
//

using System;
using System.Diagnostics;

namespace Google.Solutions.Settings
{
    /// <summary>
    /// Base class for a setting.
    /// </summary>
    public abstract class SettingBase<T> : ISetting<T>, IAnySetting
    {
        private T currentValue;

        //---------------------------------------------------------------------
        // Metadata.
        //---------------------------------------------------------------------

        public string Key { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public string Category { get; }

        public Type ValueType => typeof(T);

        //---------------------------------------------------------------------
        // Value.
        //---------------------------------------------------------------------

        public bool IsReadOnly { get; }

        public bool IsDirty { get; private set; } = false;

        public T DefaultValue { get; }

        public bool IsDefault => Equals(this.DefaultValue, this.Value);

        public bool IsSpecified { get; private set; }

        public T Value
        {
            get => this.currentValue;
            set
            {
                if (!IsValid(value))
                {
                    throw new ArgumentOutOfRangeException(
                        $"Value {value} is not within the permitted range");
                }

                this.IsDirty = !Equals(value, this.currentValue);
                this.IsSpecified = true;
                this.currentValue = value;
            }
        }

        public object AnyValue
        {
            get => this.Value;
            set
            {
                T typedValue;
                if (value is null)
                {
                    typedValue = this.DefaultValue;
                }
                else if (value is T t)
                {
                    typedValue = t;
                }
                else
                {
                    throw new InvalidCastException(
                        "Value must be of type " + typeof(T).Name);
                }

                this.Value = typedValue;
            }
        }

        public void Reset()
        {
            this.Value = this.DefaultValue;
        }

        //---------------------------------------------------------------------
        // Overlay.
        //---------------------------------------------------------------------

        public ISetting<T> OverlayBy(ISetting<T> overlaySetting) // TODO: remove overlay logic
        {
            //
            // NB. The idea of overlaying is that you use a base setting
            // (root), overlay it with a more specific setting, overlay
            // it with a more specific setting one again, etc... walking
            // your way from the root to the leaf.
            //

            Debug.Assert(overlaySetting.Key == this.Key);
            Debug.Assert(overlaySetting.DisplayName == this.DisplayName);
            Debug.Assert(overlaySetting.Description == this.Description);
            Debug.Assert(overlaySetting.Category == this.Category);

            if (!overlaySetting.IsSpecified)
            {
                //
                // Overlay does not add anything new, but own setting
                // becomes new default.
                //
                return CreateNew(
                    (T)this.Value,
                    (T)this.Value,
                    this.IsReadOnly);
            }
            else
            {
                //
                // Overlay changes the effective setting, with
                // own setting serving as the new default.
                //
                return CreateNew(
                    (T)overlaySetting.Value,
                    (T)this.Value,
                    this.IsReadOnly);
            }
        }

        //---------------------------------------------------------------------
        // Ctor.
        //---------------------------------------------------------------------

        protected SettingBase(
            string key,
            string title,
            string description,
            string category,
            T initialValue,
            T defaultValue,
            bool isSpecified,
            bool readOnly)
        {
            this.Key = key;
            this.DisplayName = title;
            this.Description = description;
            this.Category = category;
            this.currentValue = initialValue;
            this.DefaultValue = defaultValue;
            this.IsSpecified = isSpecified;
            this.IsReadOnly = readOnly;
            Debug.Assert(!this.IsDirty);
        }

        protected abstract bool IsValid(T value);

        protected abstract SettingBase<T> CreateNew( // TODO: remove overlay logic
            T value,
            T defaultValue,
            bool readOnly);

        public abstract SettingBase<T> CreateSimilar(
            T value,
            T defaultValue,
            bool isSpecified,
            bool readOnly);

        public abstract bool IsCurrentValueValid { get; }
    }
}
