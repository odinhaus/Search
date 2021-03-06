﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Data.Core.Linq
{
    public enum QueryExpressionType
    {
        MinValue = 200,
        Predicate = MinValue,
        EQ,
        Exists,
        All,
        And,
        ElemMatch,
        GeoIntersects,
        GT,
        GTE,
        In,
        LT,
        LTE,
        Matches,
        Mod,
        NE,
        Near,
        Not,
        NotExists,
        NotIn,
        Or,
        Size,
        SizeGT,
        SizeGTE,
        SizeLT,
        SizeLTE,
        Text,
        Where,
        Within,
        WithinCircle,
        WithinPolygon,
        WithinRectangle,
        Contains,
        StartsWith,
        Sort,
        NamedValue,
        Field,
        PathProjection,
        Traverse,
        OutEdgeNodeFilter,
        InEdgeNodeFilter,
        PathNodeFilterMember,
        PathEdgeFilterMember,
        PathRootFilter,
        TraverseOrigin,
        TraverseReturns,
        Save,
        Delete,
        BQL,
        Scalar,
        Date_Timestamp = 1000,
        Date_ISO8601,
        Date_DayOfWeek,
        Date_Year,
        Date_Month,
        Date_Day,
        Date_Hour,
        Date_Minute,
        Date_Second,
        Date_Millisecond,
        Date_DayOfYear,
        Date_Add,
        Date_Subtract,
        Date_Diff,

    }

    public enum DateFunctionType
    {
        Date_Timestamp = 1000,
        Date_ISO8601,
        Date_DayOfWeek,
        Date_Year,
        Date_Month,
        Date_Day,
        Date_Hour,
        Date_Minute,
        Date_Second,
        Date_Millisecond,
        Date_DayOfYear,
        Date_Add,
        Date_Subtract,
        Date_Diff,
    }
}