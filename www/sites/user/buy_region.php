<?
if (!$_SESSION[USERID]) header("Location: Index.php?page=Home");
include("settings/stardust.php");


$DbLink3 = new DB;
$DbLink3->query("SELECT RegionName FROM gridregions WHERE RegionName = '$_POST[name]'");
$DbLink2 = new DB;
$DbLink2->query("SELECT name FROM region_purchases WHERE name = '$_POST[name]'");

if ($DbLink2->num_rows() + $DbLink3->num_rows() > 0)
{
	header("Location: Index.php?page=getregion&button_id=$_POST[button_id]&id=$_POST[id]&name=$_POST[name]&notes=$_POST[notes]&error=Name already in use");
}
else
{
	$_SESSION[purchase_id] = generateGuid();
	$DbLink3->query("INSERT INTO region_purchases 
		(user_agent_id,product_id,name,notes,shape_id,order_complete,up, purchase_id, paypal_raw, complete_reference) VALUES 
		('".$_SESSION[USERID]."', ".cleanQuery($_POST[id]).", '".cleanQuery($_POST[name])."','".cleanQuery($_POST[notes])."',1,0,0, '$_SESSION[purchase_id]', '', '')");
	$DbLink3->query("SELECT name, price FROM region_products WHERE id = ".cleanQuery($_POST[id]));
	list($nameOfType, $priceOfRegion) = $DbLink3->next_record();
	$_SESSION[paypalPurchaseItem] = $nameOfType . " Region";
	$_SESSION[paypalAmount] = $priceOfRegion;
	$_SESSION[purchase_type] = "_xclick-subscriptions";
}
?>
<script language="Javascript">
	function Go()
	{
		window.location.href='send_to_paypal.php';
	}
	window.onload=Go; 
</script>