<?
##################### STARDUST #########################

$banker_id = '0b14870b-35d0-4238-8839-11d7fee7d74c';

$incompletePurchaseEmailandErrors = 'robert.skidmore@gmail.com, wendellthor@yahoo.com, djwyand@yahoo.com';

$payPalURL = 'www.sandbox.paypal.com';
$auth_token = "eK2d7-QOTrnwhqaJI2n-SpJ98p5Zb6V62lNekl8BdH-1-Q_PUCwzaDvokeW";
$payPalAccount = 'robert_1302824747_biz@gmail.com';
$notifyURL = 'http://192.168.1.170/app/StarDust/paypal_verify.php';
$returnURL = 'http://192.168.1.170/index.php?page=paypalcomplete';

$ErrorMessagePurchaseAlreadyComplete = "This purchase has already been completed. If you have not seen the currency applied yet, please be patient, as it might be on hold.";
$AmountAdditionPerfectage = 0.0291;

function generateGuid($include_braces = false) {
	if (function_exists('com_create_guid')) {
		if ($include_braces === true) {
			return com_create_guid();
		} else {
			return substr(com_create_guid(), 1, 36);
		}
	} else {
		mt_srand((double) microtime() * 10000);
		$charid = strtoupper(md5(uniqid(rand(), true)));
	   
		$guid = substr($charid,  0, 8) . '-' .
				substr($charid,  8, 4) . '-' .
				substr($charid, 12, 4) . '-' .
				substr($charid, 16, 4) . '-' .
				substr($charid, 20, 12);
 
		if ($include_braces) {
			$guid = '{' . $guid . '}';
		}
   
		return $guid;
	}
}
?>