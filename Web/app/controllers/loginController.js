﻿'use strict';
app.controller('loginController', ['$scope', '$location', 'authService', 'localStorageService', function ($scope, $location, authService, localStorageService) {

    $scope.loginData = {
        userName: "",
        password: "",
        useRefreshTokens: false
    };

    $scope.message = "";

    $scope.login = function () {

        authService.login($scope.loginData).then(function (response) {

            $location.path('/orders');

        },
         function (err) {
             $scope.message = err.error_description;
         });
    };

    if ($location.search().access_token) {
        authService.setCredentials({ token: $location.search().access_token, userName: $location.search().userName, isAuth: true });
        $location.url($location.path('/home')); // trim off the query string params
    }

}]);