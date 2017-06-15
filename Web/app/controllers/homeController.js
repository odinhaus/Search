'use strict';
app.controller('homeController', ['$scope', '$location', 'authService', function ($scope, $location, authService) {
    $scope.authentication = authService.getCredentials();
}]);