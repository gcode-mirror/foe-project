﻿using System;
using System.Collections.Generic;
using System.Text;

namespace Foe.Client
{
    public class FoeClientCatalogItem
    {
        public string Code { get; set; }
        public string ContentType { get; set; }
        public bool IsSubscribed { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DtLastUpdated { get; set; }

        public FoeClientCatalogItem(string code, string contentType, bool isSubscribed, string name, string description)
        {
            Code = code;
            ContentType = contentType;
            IsSubscribed = isSubscribed;
            Name = name;
            Description = description;
        }

        public FoeClientCatalogItem()
        {
            Code = null;
            ContentType = null;
            IsSubscribed = false;
            Name = null;
            Description = null;
        }
    }

    public class FoeClientRequestItem
    {
        public string Id { get; set; }
        public string Type { get; set; }
        public DateTime DtRequested { get; set; }
    }

    public class FoeClientRegistryEntry
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
