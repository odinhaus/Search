'use strict';
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
        localStorageService.set("authorizationData", { token: $location.search().access_token, userName: $location.search().userName });
        authService.authentication.isAuth = true;
        authService.authentication.userName = $location.search().userName;
        $location.url($location.path('/home')); // trim off the query string params
    }

}]);