// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace osu.Game.Users
{
    /// <summary>
    /// Matches `osu_countries` database table.
    /// </summary>
    [JsonConverter(typeof(StringEnumConverter))]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    public enum CountryCode
    {
        [Description("Unknown"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5397))]
        Unknown = 0,

        [Description("Anonymous Proxy"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5398))]
        A1,

        [Description("Satellite Provider"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5399))]
        A2,

        [Description("Andorra"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5400))]
        AD,

        [Description("United Arab Emirates"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5401))]
        AE,

        [Description("Afghanistan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5402))]
        AF,

        [Description("Antigua and Barbuda"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5403))]
        AG,

        [Description("Anguilla"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5404))]
        AI,

        [Description("Albania"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5405))]
        AL,

        [Description("Armenia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5406))]
        AM,

        [Description("Netherlands Antilles"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5407))]
        AN,

        [Description("Angola"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5408))]
        AO,

        [Description("Asia/Pacific Region"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5409))]
        AP,

        [Description("Antarctica"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5410))]
        AQ,

        [Description("Argentina"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5411))]
        AR,

        [Description("American Samoa"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5412))]
        AS,

        [Description("Austria"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5413))]
        AT,

        [Description("Australia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5414))]
        AU,

        [Description("Aruba"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5415))]
        AW,

        [Description("Aland Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5416))]
        AX,

        [Description("Azerbaijan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5417))]
        AZ,

        [Description("Bosnia and Herzegovina"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5418))]
        BA,

        [Description("Barbados"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5419))]
        BB,

        [Description("Bangladesh"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5420))]
        BD,

        [Description("Belgium"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5421))]
        BE,

        [Description("Burkina Faso"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5422))]
        BF,

        [Description("Bulgaria"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5423))]
        BG,

        [Description("Bahrain"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5424))]
        BH,

        [Description("Burundi"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5425))]
        BI,

        [Description("Benin"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5426))]
        BJ,

        [Description("Saint Barthelemy"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5427))]
        BL,

        [Description("Bermuda"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5428))]
        BM,

        [Description("Brunei"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5429))]
        BN,

        [Description("Bolivia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5430))]
        BO,

        [Description("Caribbean Netherlands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5431))]
        BQ,

        [Description("Brazil"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5432))]
        BR,

        [Description("Bahamas"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5433))]
        BS,

        [Description("Bhutan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5434))]
        BT,

        [Description("Bouvet Island"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5435))]
        BV,

        [Description("Botswana"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5436))]
        BW,

        [Description("Belarus"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5437))]
        BY,

        [Description("Belize"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5438))]
        BZ,

        [Description("Canada"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5439))]
        CA,

        [Description("Cocos (Keeling) Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5440))]
        CC,

        [Description("The Democratic Republic of the Congo"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5441))]
        CD,

        [Description("Central African Republic"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5442))]
        CF,

        [Description("Congo"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5443))]
        CG,

        [Description("Switzerland"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5444))]
        CH,

        [Description("Cote D'Ivoire"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5445))]
        CI,

        [Description("Cook Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5446))]
        CK,

        [Description("Chile"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5447))]
        CL,

        [Description("Cameroon"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5448))]
        CM,

        [Description("China"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5449))]
        CN,

        [Description("Colombia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5450))]
        CO,

        [Description("Costa Rica"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5451))]
        CR,

        [Description("Cuba"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5452))]
        CU,

        [Description("Cabo Verde"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5453))]
        CV,

        [Description("Curaçao"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5454))]
        CW,

        [Description("Christmas Island"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5455))]
        CX,

        [Description("Cyprus"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5456))]
        CY,

        [Description("Czechia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5457))]
        CZ,

        [Description("Germany"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5458))]
        DE,

        [Description("Djibouti"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5459))]
        DJ,

        [Description("Denmark"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5460))]
        DK,

        [Description("Dominica"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5461))]
        DM,

        [Description("Dominican Republic"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5462))]
        DO,

        [Description("Algeria"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5463))]
        DZ,

        [Description("Ecuador"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5464))]
        EC,

        [Description("Estonia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5465))]
        EE,

        [Description("Egypt"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5466))]
        EG,

        [Description("Western Sahara"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5467))]
        EH,

        [Description("Eritrea"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5468))]
        ER,

        [Description("Spain"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5469))]
        ES,

        [Description("Ethiopia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5470))]
        ET,

        [Description("Europe"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5471))]
        EU,

        [Description("Finland"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5472))]
        FI,

        [Description("Fiji"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5473))]
        FJ,

        [Description("Falkland Islands (Malvinas)"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5474))]
        FK,

        [Description("Federated States of Micronesia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5475))]
        FM,

        [Description("Faroe Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5476))]
        FO,

        [Description("France"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5477))]
        FR,

        [Description("France, Metropolitan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5478))]
        FX,

        [Description("Gabon"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5479))]
        GA,

        [Description("United Kingdom"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5480))]
        GB,

        [Description("Grenada"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5481))]
        GD,

        [Description("Georgia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5482))]
        GE,

        [Description("French Guiana"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5483))]
        GF,

        [Description("Guernsey"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5484))]
        GG,

        [Description("Ghana"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5485))]
        GH,

        [Description("Gibraltar"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5486))]
        GI,

        [Description("Greenland"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5487))]
        GL,

        [Description("Gambia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5488))]
        GM,

        [Description("Guinea"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5489))]
        GN,

        [Description("Guadeloupe"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5490))]
        GP,

        [Description("Equatorial Guinea"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5491))]
        GQ,

        [Description("Greece"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5492))]
        GR,

        [Description("South Georgia and the South Sandwich Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5493))]
        GS,

        [Description("Guatemala"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5494))]
        GT,

        [Description("Guam"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5495))]
        GU,

        [Description("Guinea-Bissau"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5496))]
        GW,

        [Description("Guyana"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5497))]
        GY,

        [Description("Hong Kong"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5498))]
        HK,

        [Description("Heard Island and McDonald Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5499))]
        HM,

        [Description("Honduras"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5500))]
        HN,

        [Description("Croatia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5501))]
        HR,

        [Description("Haiti"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5502))]
        HT,

        [Description("Hungary"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5503))]
        HU,

        [Description("Indonesia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5504))]
        ID,

        [Description("Ireland"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5505))]
        IE,

        [Description("Israel"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5506))]
        IL,

        [Description("Isle of Man"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5507))]
        IM,

        [Description("India"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5508))]
        IN,

        [Description("British Indian Ocean Territory"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5509))]
        IO,

        [Description("Iraq"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5510))]
        IQ,

        [Description("Islamic Republic of Iran"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5511))]
        IR,

        [Description("Iceland"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5512))]
        IS,

        [Description("Italy"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5513))]
        IT,

        [Description("Jersey"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5514))]
        JE,

        [Description("Jamaica"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5515))]
        JM,

        [Description("Jordan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5516))]
        JO,

        [Description("Japan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5517))]
        JP,

        [Description("Kenya"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5518))]
        KE,

        [Description("Kyrgyzstan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5519))]
        KG,

        [Description("Cambodia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5520))]
        KH,

        [Description("Kiribati"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5521))]
        KI,

        [Description("Comoros"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5522))]
        KM,

        [Description("Saint Kitts and Nevis"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5523))]
        KN,

        [Description("Democratic People's Republic of Korea"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5524))]
        KP,

        [Description("South Korea"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5525))]
        KR,

        [Description("Kuwait"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5526))]
        KW,

        [Description("Cayman Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5527))]
        KY,

        [Description("Kazakhstan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5528))]
        KZ,

        [Description("Lao People's Democratic Republic"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5529))]
        LA,

        [Description("Lebanon"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5530))]
        LB,

        [Description("Saint Lucia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5531))]
        LC,

        [Description("Liechtenstein"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5532))]
        LI,

        [Description("Sri Lanka"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5533))]
        LK,

        [Description("Liberia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5534))]
        LR,

        [Description("Lesotho"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5535))]
        LS,

        [Description("Lithuania"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5536))]
        LT,

        [Description("Luxembourg"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5537))]
        LU,

        [Description("Latvia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5538))]
        LV,

        [Description("Libya"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5539))]
        LY,

        [Description("Morocco"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5540))]
        MA,

        [Description("Monaco"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5541))]
        MC,

        [Description("Moldova"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5542))]
        MD,

        [Description("Montenegro"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5543))]
        ME,

        [Description("Saint Martin"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5544))]
        MF,

        [Description("Madagascar"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5545))]
        MG,

        [Description("Marshall Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5546))]
        MH,

        [Description("North Macedonia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5547))]
        MK,

        [Description("Mali"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5548))]
        ML,

        [Description("Myanmar"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5549))]
        MM,

        [Description("Mongolia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5550))]
        MN,

        [Description("Macau"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5551))]
        MO,

        [Description("Northern Mariana Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5552))]
        MP,

        [Description("Martinique"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5553))]
        MQ,

        [Description("Mauritania"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5554))]
        MR,

        [Description("Montserrat"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5555))]
        MS,

        [Description("Malta"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5556))]
        MT,

        [Description("Mauritius"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5557))]
        MU,

        [Description("Maldives"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5558))]
        MV,

        [Description("Malawi"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5559))]
        MW,

        [Description("Mexico"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5560))]
        MX,

        [Description("Malaysia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5561))]
        MY,

        [Description("Mozambique"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5562))]
        MZ,

        [Description("Namibia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5563))]
        NA,

        [Description("New Caledonia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5564))]
        NC,

        [Description("Niger"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5565))]
        NE,

        [Description("Norfolk Island"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5566))]
        NF,

        [Description("Nigeria"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5567))]
        NG,

        [Description("Nicaragua"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5568))]
        NI,

        [Description("Netherlands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5569))]
        NL,

        [Description("Norway"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5570))]
        NO,

        [Description("Nepal"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5571))]
        NP,

        [Description("Nauru"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5572))]
        NR,

        [Description("Niue"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5573))]
        NU,

        [Description("New Zealand"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5574))]
        NZ,

        [Description("Other"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5575))]
        O1,

        [Description("Oman"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5576))]
        OM,

        [Description("Panama"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5577))]
        PA,

        [Description("Peru"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5578))]
        PE,

        [Description("French Polynesia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5579))]
        PF,

        [Description("Papua New Guinea"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5580))]
        PG,

        [Description("Philippines"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5581))]
        PH,

        [Description("Pakistan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5582))]
        PK,

        [Description("Poland"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5583))]
        PL,

        [Description("Saint Pierre and Miquelon"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5584))]
        PM,

        [Description("Pitcairn"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5585))]
        PN,

        [Description("Puerto Rico"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5586))]
        PR,

        [Description("State of Palestine"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5587))]
        PS,

        [Description("Portugal"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5588))]
        PT,

        [Description("Palau"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5589))]
        PW,

        [Description("Paraguay"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5590))]
        PY,

        [Description("Qatar"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5591))]
        QA,

        [Description("Reunion"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5592))]
        RE,

        [Description("Romania"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5593))]
        RO,

        [Description("Serbia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5594))]
        RS,

        [Description("Russian Federation"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5595))]
        RU,

        [Description("Rwanda"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5596))]
        RW,

        [Description("Saudi Arabia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5597))]
        SA,

        [Description("Solomon Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5598))]
        SB,

        [Description("Seychelles"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5599))]
        SC,

        [Description("Sudan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5600))]
        SD,

        [Description("Sweden"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5601))]
        SE,

        [Description("Singapore"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5602))]
        SG,

        [Description("Saint Helena"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5603))]
        SH,

        [Description("Slovenia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5604))]
        SI,

        [Description("Svalbard and Jan Mayen"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5605))]
        SJ,

        [Description("Slovakia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5606))]
        SK,

        [Description("Sierra Leone"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5607))]
        SL,

        [Description("San Marino"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5608))]
        SM,

        [Description("Senegal"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5609))]
        SN,

        [Description("Somalia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5610))]
        SO,

        [Description("Suriname"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5611))]
        SR,

        [Description("Sao Tome and Principe"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5612))]
        ST,

        [Description("El Salvador"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5613))]
        SV,

        [Description("Sint Maarten"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5614))]
        SX,

        [Description("Syrian Arab Republic"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5615))]
        SY,

        [Description("Eswatini"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5616))]
        SZ,

        [Description("Turks and Caicos Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5617))]
        TC,

        [Description("Chad"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5618))]
        TD,

        [Description("French Southern Territories"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5619))]
        TF,

        [Description("Togo"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5620))]
        TG,

        [Description("Thailand"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5621))]
        TH,

        [Description("Tajikistan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5622))]
        TJ,

        [Description("Tokelau"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5623))]
        TK,

        [Description("Timor-Leste"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5624))]
        TL,

        [Description("Turkmenistan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5625))]
        TM,

        [Description("Tunisia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5626))]
        TN,

        [Description("Tonga"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5627))]
        TO,

        [Description("Türkiye"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5628))]
        TR,

        [Description("Trinidad and Tobago"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5629))]
        TT,

        [Description("Tuvalu"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5630))]
        TV,

        [Description("Taiwan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5631))]
        TW,

        [Description("United Republic of Tanzania"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5632))]
        TZ,

        [Description("Ukraine"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5633))]
        UA,

        [Description("Uganda"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5634))]
        UG,

        [Description("United States Minor Outlying Islands"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5635))]
        UM,

        [Description("United States"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5636))]
        US,

        [Description("Uruguay"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5637))]
        UY,

        [Description("Uzbekistan"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5638))]
        UZ,

        [Description("Holy See (Vatican City State)"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5639))]
        VA,

        [Description("Saint Vincent and the Grenadines"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5640))]
        VC,

        [Description("Venezuela"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5641))]
        VE,

        [Description("Virgin Islands, British"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5642))]
        VG,

        [Description("Virgin Islands, U.S."), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5643))]
        VI,

        [Description("Vietnam"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5644))]
        VN,

        [Description("Vanuatu"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5645))]
        VU,

        [Description("Wallis and Futuna"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5646))]
        WF,

        [Description("Samoa"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5647))]
        WS,

        [Description("Kosovo"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5648))]
        XK,

        [Description("Yemen"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5649))]
        YE,

        [Description("Mayotte"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5650))]
        YT,

        [Description("South Africa"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5651))]
        ZA,

        [Description("Zambia"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5652))]
        ZM,

        [Description("Zimbabwe"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5653))]
        ZW,
    }
}
