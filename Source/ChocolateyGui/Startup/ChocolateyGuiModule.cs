﻿// --------------------------------------------------------------------------------------------------------------------
// <copyright company="Chocolatey" file="ChocolateyGuiModule.cs">
//   Copyright 2014 - Present Rob Reynolds, the maintainers of Chocolatey, and RealDimensions Software, LLC
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.ComponentModel;
using System.IO;
using Autofac;
using AutoMapper;
using Caliburn.Micro;
using chocolatey;
using chocolatey.infrastructure.app.configuration;
using chocolatey.infrastructure.app.services;
using chocolatey.infrastructure.filesystem;
using chocolatey.infrastructure.services;
using ChocolateyGui.Common.Models;
using ChocolateyGui.Common.Providers;
using ChocolateyGui.Common.Services;
using ChocolateyGui.Common.ViewModels.Items;
using ChocolateyGui.Services;
using ChocolateyGui.ViewModels;
using ChocolateyGui.Views;
using LiteDB;
using MahApps.Metro.Controls.Dialogs;
using NuGet;
using ChocolateySource = chocolatey.infrastructure.app.configuration.ChocolateySource;
using PackageViewModel = ChocolateyGui.ViewModels.Items.PackageViewModel;

namespace ChocolateyGui.Startup
{
    internal class ChocolateyGuiModule : Module
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Not Relevant")]
        protected override void Load(ContainerBuilder builder)
        {
            var viewModelAssembly = typeof(ShellViewModel).Assembly;
            var viewAssembly = typeof(ShellView).Assembly;

            // Register Providers
            builder.RegisterType<VersionNumberProvider>().As<IVersionNumberProvider>().SingleInstance();
            builder.RegisterType<Elevation>().SingleInstance();
            builder.RegisterType<ChocolateyConfigurationProvider>().As<IChocolateyConfigurationProvider>().SingleInstance();
            builder.RegisterType<ChocolateyService>().As<IChocolateyService>().SingleInstance();
            builder.RegisterType<DotNetFileSystem>().As<chocolatey.infrastructure.filesystem.IFileSystem>().SingleInstance();

            // Register ViewModels
            builder.RegisterAssemblyTypes(viewModelAssembly)
                .Where(type => type.Name.EndsWith("ViewModel", StringComparison.Ordinal))
                .Where(type => type.GetInterface(typeof(INotifyPropertyChanged).Name) != null)
                .AsSelf()
                .InstancePerDependency();

            builder.RegisterType<PackageViewModel>().As<IPackageViewModel>();

            var choco = Lets.GetChocolatey();
            builder.RegisterInstance(choco.Container().GetInstance<IChocolateyConfigSettingsService>())
                .As<IChocolateyConfigSettingsService>().SingleInstance();
            builder.RegisterInstance(choco.Container().GetInstance<IXmlService>())
                .As<IXmlService>().SingleInstance();

            // Register Views
            builder.RegisterAssemblyTypes(viewAssembly)
                .Where(type => type.Name.EndsWith("View", StringComparison.Ordinal))
                .AsSelf()
                .InstancePerDependency();

            // Register the single window manager for this container
            builder.Register<IWindowManager>(c => new WindowManager()).InstancePerLifetimeScope();

            // Register the single event aggregator for this container
            builder.Register<IEventAggregator>(c => new EventAggregator()).InstancePerLifetimeScope();

            // Register Services
            builder.RegisterType<ProgressService>().As<IProgressService>().SingleInstance();
            builder.RegisterType<PersistenceService>().As<IPersistenceService>().SingleInstance();
            builder.RegisterType<LiteDBFileStorageService>().As<IFileStorageService>().SingleInstance();
            builder.RegisterType<ConfigService>().As<IConfigService>().SingleInstance();
            builder.RegisterType<ChocolateyGuiCacheService>().As<IChocolateyGuiCacheService>().SingleInstance();

            // Register Mapper
            var mapperConfiguration = new MapperConfiguration(config =>
            {
                config.CreateMap<IPackageViewModel, IPackageViewModel>()
                    .ForMember(vm => vm.IsInstalled, options => options.Ignore());

                config.CreateMap<DataServicePackage, Package>()
                    .ForMember(dest => dest.Authors, opt => opt.MapFrom(src => src.Authors.Split(',')))
                    .ForMember(dest => dest.Owners, opt => opt.MapFrom(src => src.Owners.Split(',')));
                config.CreateMap<IPackage, Package>();

                config.CreateMap<ConfigFileFeatureSetting, ChocolateyFeature>();
                config.CreateMap<ConfigFileConfigSetting, ChocolateySetting>();
                config.CreateMap<ConfigFileSourceSetting, Common.Models.ChocolateySource>()
                    .ForMember(dest => dest.Password, opt => opt.MapFrom(src => NugetEncryptionUtility.DecryptString(src.Password)))
                    .ForMember(dest => dest.CertificatePassword, opt => opt.MapFrom(src => NugetEncryptionUtility.DecryptString(src.CertificatePassword)));

                config.CreateMap<ChocolateySource, Common.Models.ChocolateySource>()
                    .ForMember(dest => dest.VisibleToAdminsOnly, opt => opt.MapFrom(src => src.VisibleToAdminOnly));
            });

            builder.RegisterInstance(DialogCoordinator.Instance).As<IDialogCoordinator>();
            builder.RegisterInstance(mapperConfiguration.CreateMapper()).As<IMapper>();

            var database = new LiteDatabase($"filename={Path.Combine(Bootstrapper.LocalAppDataPath, "data.db")};upgrade=true");
            builder.Register(c => database).SingleInstance();

            // Commands
            builder.RegisterType<FeatureCommand>().As<ICommand>().SingleInstance();
            builder.RegisterType<ConfigCommand>().As<ICommand>().SingleInstance();
            builder.RegisterType<PurgeCommand>().As<ICommand>().SingleInstance();
        }
    }
}