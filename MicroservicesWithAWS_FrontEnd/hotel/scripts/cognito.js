const config={
    cognito:{
        identityPoolId:"ap-southeast-2_h6tbvGgxB",
        cognitoDomain:"ap-southeast-2h6tbvggxb.auth.ap-southeast-2.amazoncognito.com",
        appId:"2rgtga2b1qd4isrsinrkfsev4i"
    },
    assets:{
        // 配置你的公开图片域名（CloudFront 或公开 S3），不要带最后的斜杠
        // 例如：
        //   https://dxxxxx.cloudfront.net
        //   https://your-bucket.s3.ap-southeast-2.amazonaws.com
        imageBaseUrl: "https://hotel-admin-bucket-awang-2025.s3.ap-southeast-2.amazonaws.com"
    }
}

var cognitoApp={
    auth:{},
    Init: function()
    {

        var authData = {
            ClientId : config.cognito.appId,
            AppWebDomain : config.cognito.cognitoDomain,
            TokenScopesArray : ['email', 'openid','profile'],
            RedirectUriSignIn : 'http://localhost:8080/hotel',
            RedirectUriSignOut : 'http://localhost:8080/hotel',
            UserPoolId : config.cognito.identityPoolId, 
            AdvancedSecurityDataCollectionFlag : false,
                Storage: null
        };

        cognitoApp.auth = new AmazonCognitoIdentity.CognitoAuth(authData);
        cognitoApp.auth.userhandler = {
            onSuccess: function(result) {
              
            },
            onFailure: function(err) {
            }
        };
    }
}