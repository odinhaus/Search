﻿
var app = angular.module('AngularAuthApp', ['ngRoute', 'LocalStorageModule', 'angular-loading-bar']);

app.config(function ($routeProvider) {

    $routeProvider.when("/home", {
        controller: "homeController",
        templateUrl: "/app/views/home.html"
    });

    $routeProvider.when("/login", {
        controller: "loginController",
        templateUrl: "/app/views/login.html"
    });

    $routeProvider.when("/signup", {
        controller: "signupController",
        templateUrl: "/app/views/signup.html"
    });

    $routeProvider.when("/signup/customer", {
        controller: "customerSignupController",
        templateUrl: "/app/views/signup-customer.html"
    });

    $routeProvider.when("/orders", {
        controller: "ordersController",
        templateUrl: "/app/views/orders.html"
    });

    $routeProvider.when("/refresh", {
        controller: "refreshController",
        templateUrl: "/app/views/refresh.html"
    });

    $routeProvider.when("/tokens", {
        controller: "tokensManagerController",
        templateUrl: "/app/views/tokens.html"
    });

    $routeProvider.when("/pricing", {
        controller: "pricingController",
        templateUrl: "/app/views/pricing.html"
    });

    $routeProvider.otherwise({ redirectTo: "/home" });

});

app.constant('ngAuthSettings', {    
    apiServiceBaseUri: 'http://localhost:9000/',
    //apiServiceBaseUri: 'http://ngauthenticationapi.azurewebsites.net/',
    clientId: 'Suffuz',
    clientSecret: '123@abc',
    deviceId: 'mydevice'
});

app.config(function ($httpProvider) {
    $httpProvider.interceptors.push('authInterceptorService');
});

app.run(['authService', function (authService) {
    authService.fillAuthData();
}]);

