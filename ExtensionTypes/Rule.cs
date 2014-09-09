﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Rule.cs" company="">
//   
// </copyright>
// <summary>
//   The rule.
// </summary>
// --------------------------------------------------------------------------------------------------------------------
namespace ExtensionTypes
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using PropertyChanged;

    /// <summary>
    ///     The rule.
    /// </summary>
    [ImplementPropertyChanged]
    [Serializable]
    public class Rule : ICloneable
    {
        #region Fields

        /// <summary>
        ///     The characteristics.
        /// </summary>
        private readonly List<Characteristic> characteristics = SqaleDefaultModel.Model;

        /// <summary>
        ///     The allowedsub.
        /// </summary>
        private ObservableCollection<SubCategory> allowedsub = new ObservableCollection<SubCategory>();

        /// <summary>
        ///     The categ.
        /// </summary>
        private Category categ = Category.UNDEFINED;

        /// <summary>
        /// The remediation function.
        /// </summary>
        private RemediationFunction remediationFunction;

        /// <summary>
        /// The subcategory.
        /// </summary>
        private SubCategory subcategory;

        #endregion

        #region Constructors and Destructors

        /// <summary>
        ///     Initializes a new instance of the <see cref="Rule" /> class.
        /// </summary>
        public Rule()
        {
            this.EnableSetDeafaults = true;
            this.Enabled = true;

            this.RemediationFactorVal = 0;
            this.RemediationOffsetVal = 0;
            this.RemediationFunction = RemediationFunction.UNDEFINED;
            this.RemediationOffsetTxt = RemediationUnit.UNDEFINED;
            this.RemediationFactorTxt = RemediationUnit.UNDEFINED;
            this.Category = Category.UNDEFINED;
            this.Subcategory = SubCategory.UNDEFINED;
            this.Severity = Severity.UNDEFINED;

            this.Tags = new ObservableCollection<string>();
            this.SysTags = new ObservableCollection<string>();
            this.Params = new ObservableCollection<RuleParam>();
        }

        #endregion

        #region Public Properties

        /// <summary>
        ///     Gets or sets the category.
        /// </summary>
        [AlsoNotifyFor("Subcategory")]
        public Category Category
        {
            get
            {
                return this.categ;
            }

            set
            {
                this.categ = value;

                if (value == Category.UNDEFINED)
                {
                    this.subcategory = SubCategory.UNDEFINED;
                    this.SubcategoryValues.Clear();
                    return;
                }

                foreach (Characteristic characteristic in this.characteristics)
                {
                    if (characteristic.Key.Equals(value))
                    {
                        this.SubcategoryValues.Clear();
                        foreach (SubCharacteristics subcategoryValue in characteristic.Subchars)
                        {
                            this.SubcategoryValues.Add(subcategoryValue.Key);
                        }

                        this.SubcategoryValues.Add(SubCategory.UNDEFINED);
                    }
                }

                if (this.EnableSetDeafaults)
                {
                    // set defaults
                    if (this.SubcategoryValues.Count != 0)
                    {
                        this.Subcategory = this.SubcategoryValues[0];
                    }

                    if (this.RemediationFunction.Equals(RemediationFunction.UNDEFINED))
                    {
                        this.RemediationFunction = RemediationFunction.LINEAR;

                        if (this.RemediationFactorTxt.Equals(RemediationUnit.UNDEFINED))
                        {
                            this.RemediationFactorTxt = RemediationUnit.MN;
                        }

                        this.RemediationOffsetTxt = RemediationUnit.UNDEFINED;

                        if (this.RemediationFactorVal == 0 || this.RemediationFactorVal == 0 || this.RemediationFactorVal == 0)
                        {
                            this.RemediationFactorVal = 5;
                        }
                    }
                }
            }
        }

        /// <summary>
        ///     Gets or sets the severity.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        ///     Gets or sets the severity.
        /// </summary>
        public string CategoryName { get; set; }

        /// <summary>
        ///     Gets or sets the config key.
        /// </summary>
        public string ConfigKey { get; set; }

        /// <summary>
        ///     Gets or sets the repo.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether debt overloaded.
        /// </summary>
        public bool DebtOverloaded { get; set; }

        /// <summary>
        ///     Gets or sets the debt rem fn coeff.
        /// </summary>
        public string DebtRemFnCoeff { get; set; }

        /// <summary>
        ///     Gets or sets the default debt char.
        /// </summary>
        public Category DefaultDebtChar { get; set; }

        /// <summary>
        ///     Gets or sets the default debt rem fn coeff.
        /// </summary>
        public string DefaultDebtRemFnCoeff { get; set; }

        /// <summary>
        ///     Gets or sets the default debt rem fn type.
        /// </summary>
        public RemediationFunction DefaultDebtRemFnType { get; set; }

        /// <summary>
        ///     Gets or sets the default debt sub char.
        /// </summary>
        public SubCategory DefaultDebtSubChar { get; set; }

        /// <summary>
        ///     Gets or sets the description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether enable set deafaults.
        /// </summary>
        public bool EnableSetDeafaults { get; set; }

        /// <summary>
        ///     Gets or sets the internal key.
        /// </summary>
        public string InternalKey { get; set; }

        /// <summary>
        ///     Gets or sets a value indicating whether is template.
        /// </summary>
        public bool IsTemplate { get; set; }

        /// <summary>
        ///     Gets or sets the key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        ///     Gets or sets the lang.
        /// </summary>
        public string Lang { get; set; }

        /// <summary>
        ///     Gets or sets the lang name.
        /// </summary>
        public string LangName { get; set; }

        /// <summary>
        ///     Gets or sets the name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     Gets or sets the params.
        /// </summary>
        public ObservableCollection<RuleParam> Params { get; set; }

        /// <summary>
        ///     Gets or sets the remediation factor txt.
        /// </summary>
        public RemediationUnit RemediationFactorTxt { get; set; }

        /// <summary>
        ///     Gets or sets the remediation factor val.
        /// </summary>
        public int RemediationFactorVal { get; set; }

        /// <summary>
        ///     Gets or sets the remediation function.
        /// </summary>
        public RemediationFunction RemediationFunction
        {
            get
            {
                return this.remediationFunction;
            }

            set
            {
                if (this.EnableSetDeafaults)
                {
                    if (value.Equals(RemediationFunction.LINEAR))
                    {
                        this.SetFactorDefaultValue();
                        this.RemediationOffsetVal = 0;
                        this.RemediationOffsetTxt = RemediationUnit.UNDEFINED;
                    }

                    if (value.Equals(RemediationFunction.CONSTANT_ISSUE))
                    {
                        this.SetOffsetDefaultValue();

                        this.RemediationFactorVal = 0;
                        this.RemediationFactorTxt = RemediationUnit.UNDEFINED;
                    }

                    if (value.Equals(RemediationFunction.LINEAR_OFFSET))
                    {
                        this.SetFactorDefaultValue();
                        this.SetOffsetDefaultValue();
                    }
                }

                this.remediationFunction = value;
            }
        }

        /// <summary>
        ///     Gets or sets the remediation offset txt.
        /// </summary>
        public RemediationUnit RemediationOffsetTxt { get; set; }

        /// <summary>
        ///     Gets or sets the remediation offset val.
        /// </summary>
        public int RemediationOffsetVal { get; set; }

        /// <summary>
        ///     Gets or sets the repo.
        /// </summary>
        public string Repo { get; set; }

        /// <summary>
        ///     Gets or sets the severity.
        /// </summary>
        public Severity Severity { get; set; }

        /// <summary>
        ///     Gets or sets the repo.
        /// </summary>
        public Status Status { get; set; }

        /// <summary>
        ///     Gets or sets the subcategory.
        /// </summary>
        public SubCategory Subcategory
        {
            get
            {
                return this.subcategory;
            }

            set
            {
                this.subcategory = value;
                if (value == SubCategory.UNDEFINED)
                {
                    this.Category = Category.UNDEFINED;
                }
            }
        }

        /// <summary>
        ///     Gets or sets the subcategory.
        /// </summary>
        public string SubcategoryName { get; set; }

        /// <summary>
        ///     Gets or sets the subcategory values.
        /// </summary>
        public ObservableCollection<SubCategory> SubcategoryValues
        {
            get
            {
                return this.allowedsub;
            }

            set
            {
                this.allowedsub = value;
            }
        }

        /// <summary>
        ///     Gets or sets the sys tags.
        /// </summary>
        public ObservableCollection<string> SysTags { get; set; }

        /// <summary>
        ///     Gets or sets the tags.
        /// </summary>
        public ObservableCollection<string> Tags { get; set; }

        #endregion

        #region Public Methods and Operators

        /// <summary>
        ///     The clone.
        /// </summary>
        /// <returns>
        ///     The <see cref="object" />.
        /// </returns>
        public object Clone()
        {
            return this.MemberwiseClone();
        }

        /// <summary>
        /// The merge rule.
        /// </summary>
        /// <param name="rule">
        /// The rule.
        /// </param>
        public void MergeRule(Rule rule)
        {
            this.Key = rule.Key;
            this.Name = rule.Name;
            this.Description = rule.Description;
            this.ConfigKey = rule.ConfigKey;
            this.Category = rule.Category;
            this.Subcategory = rule.Subcategory;
            this.RemediationFactorTxt = rule.RemediationFactorTxt;
            this.RemediationFunction = rule.RemediationFunction;
            this.RemediationOffsetTxt = rule.RemediationOffsetTxt;
            this.Severity = rule.Severity;
            this.RemediationOffsetVal = rule.RemediationOffsetVal;
            this.RemediationFactorVal = rule.RemediationFactorVal;
            this.Repo = rule.Repo;
        }

        #endregion

        #region Methods

        /// <summary>
        /// The set factor default value.
        /// </summary>
        private void SetFactorDefaultValue()
        {
            if (this.RemediationFactorVal == 0)
            {
                this.RemediationFactorVal = 5;
            }

            if (this.RemediationFactorTxt.Equals(RemediationUnit.UNDEFINED))
            {
                this.RemediationFactorTxt = RemediationUnit.MN;
            }
        }

        /// <summary>
        /// The set offset default value.
        /// </summary>
        private void SetOffsetDefaultValue()
        {
            if (this.RemediationOffsetVal == 0)
            {
                this.RemediationOffsetVal = 1;
            }

            if (this.RemediationOffsetTxt.Equals(RemediationUnit.UNDEFINED))
            {
                this.RemediationOffsetTxt = RemediationUnit.MN;
            }
        }

        #endregion
    }
}