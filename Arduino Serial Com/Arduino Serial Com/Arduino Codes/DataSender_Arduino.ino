bool isDone = false;
char * message;
char * cntStr; 
int cnt = 0;

void setup() 
{
  Serial.begin(500000);                          //Recieving End must set to Baudrate of 500000
  message = (char*)malloc(sizeof(char)*25);
  cntStr = (char*)malloc(sizeof(char)*4);

  pinMode(LED_BUILTIN, OUTPUT);
}

void loop() 
{  
  if(!isDone)
  {
    //Serial.println("ONE");
    sprintf(cntStr," %d",++cnt);
    message = "abcdefghti";
    //message = strncat(message, cntStr,sizeof(char)*(strlen(cntStr)+1));
    Serial.flush();  
    Serial.write(message,sizeof(char)*(strlen(message)+1));   
    delay(250);
    //cntStr = '\0';
  }else{ digitalWrite(LED_BUILTIN,HIGH); }
}
