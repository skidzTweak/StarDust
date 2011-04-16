<?
include("settings/stardust.php");


if ($_GET[purchase_id] != "")
{
	$_SESSION[purchase_id] = cleanQuery($_GET[purchase_id]);
}
	
if (!$_SESSION[USERID])
{
	$_SESSION[next_page] = 'getcurrency';
	header("Location: Index.php?page=login&btn=webui_menu_item_login&next_page=getcurrency&purchase_id=$_GET[purchase_id]");
}

if ($_SESSION[purchase_id] != '')
{
	$DbLink->query("SELECT PurchaseID, PrincipalID, UserName, Amount, ConversionFactor, RegionName, RegionID, RegionPos, Complete, CompleteMethod, CompleteReference, TransactionID, Created, Updated from usercurrency_purchased where PurchaseID = '$_SESSION[purchase_id]'");
	list($PurchaseID, $PrincipalID, $UserName, $Amount, $ConversionFactor, $RegionName, $RegionID, $RegionPos, $Complete, $CompleteMethod, $CompleteReference, $TransactionID, $Created, $Updated) = $DbLink->next_record();
	if ($Complete == 1)
	{
		echo($ErrorMessagePurchaseAlreadyComplete);
		$_Session[purchase_id] = "";
	}
	else if ($PrincipalID != $_SESSION[USERID])
	{ 
	?>
		You are logged in as the wrong users for this purchase.</br>
		<b>How to fix this:</b><br/>
		1) Please logout<br/>
		2) Log back in as the correct user<br/>
		3) Then re-open the URL given to you in your local chat window.<br/>
		<br/>
	<?
	}
	else
	{
		$_SESSION[paypalAmount] = round(((($Amount / $ConversionFactor)) + (($Amount / $ConversionFactor) * $AmountAdditionPerfectage) + 0.3) * 100) / 100.0;
		$_SESSION[paypalPurchaseItem] = "G$ Currency Purchase";
		$_SESSION[purchase_type] = "_xclick";
		?>
		<table>
			<thead>
				<tr>
					<td>
						<h1>Purchase G$</h1>
					</td>
				</tr>
			</thead>
			<tr>
				<td colspan="2">Please review your purchase. If you are statified with it please click the Purchase Button.</td>
			</tr>
			<tr class="odd">
				<td>You are buying</td>
				<td>G$<?=$Amount?></td>
			</tr>
			<tr class="even">
				<td>You are paying</td>
				<td>$<?=$_SESSION[paypalAmount]?> USD</td>
			</tr>
			<tr class="odd">
				<td colspan="2">That is G$<?=$ConversionFactor?> Per Dollar</td>
			</tr>
			<tr class="even">
				<td colspan="2">* An addition fee of <?=($AmountAdditionPerfectage * 100)?>% + $0.30 has also been applied.</td>
			</tr>
			<tr>
				<td colspan="2">
					<a href="send_to_paypal.php"><img align="right" style="float:right" src="images/StarDust/paypal-purchase-button.png" /></a>
				</td>
			</tr>
			<tr>
				<td colspan="2"><h2>FAQ</h2></td>
			</tr>
			<tr class="odd">
				<td colspan="2">Q: How long does it take to get my G$?</td>
			</tr>
			<tr class="even">
				<td colspan="2">A: In most cases your G$ will be sent to you right away.<br/>
				If there are any complication during the purchase they could be placed on hold, but we will resolve the issue ASAP!</td>
			</tr>
			<tr class="odd">
				<td colspan="2">Q: Why do you charge any addition <?=($AmountAdditionPerfectage * 100)?>%?</td>
			</tr>
			<tr class="even">
				<td colspan="2">A: These are just fees that PayPal Charges us.</td>
			</tr>
			<tr class="odd">
				<td colspan="2">Q: How much are my G$ worth?</td>
			</tr>
			<tr class="even">
				<td colspan="2">A: Every <?=$ConversionFactor?> G$ are worth $1 USD, or L$250</td>
			</tr>
		</table>
		<?
	}
}
else
{
	?>
	<table>
		<thead>
			<tr>
				<td>
					<h1>Purchase G$</h1>
				</td>
			</tr>
		</thead>
		<tr>
			<td>
				If you would like to purchase G$, please login to your viewer and click you G$ Balance. <br/>Web interface to get G$ coming soon.
			</td>
		</tr>
	</table>
	<?
}

?>

